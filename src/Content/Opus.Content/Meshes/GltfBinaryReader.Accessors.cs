using System;
using System.Buffers.Binary;
using System.IO;
using System.Numerics;

namespace Opus.Content.Meshes;

/// <summary>Accessor-level byte readers used by the scene + primitive parser: typed
/// fixed-stride array reads for VEC2/VEC3/VEC4 (always float in the engine's supported
/// glTF subset) and index reads for SCALAR (8/16/32-bit unsigned). All readers go through
/// <see cref="ResolveAccessor"/> for type + component-type validation; <see cref="ResolveAccessorOffset"/>
/// resolves the bufferView byte offset, and <see cref="ValidateAccessorByteRange"/> proves the
/// requested element range fits inside the binary buffer before any array is allocated.</summary>
public static partial class GltfBinaryReader
{
    private const int ComponentTypeUbyte = 5121;
    private const int ComponentTypeUshort = 5123;
    private const int ComponentTypeUint = 5125;
    private const int ComponentTypeFloat = 5126;

    private const int Vec2ByteSize = 8;
    private const int Vec3ByteSize = 12;
    private const int Vec4ByteSize = 16;
    private const int FloatByteSize = 4;
    private const int UshortByteSize = 2;
    private const int UintByteSize = 4;
    private const int UbyteByteSize = 1;

    private static Vector3[] ReadVec3(GltfDocument doc, byte[] bin, int accessorIdx)
    {
        var (offset, count) = ResolveAccessor(doc, accessorIdx, "VEC3", ComponentTypeFloat);
        ValidateAccessorByteRange(offset, count, Vec3ByteSize, bin);
        var result = new Vector3[count];
        for (var i = 0; i < count; i++)
        {
            var o = offset + (i * Vec3ByteSize);
            result[i] = new Vector3(
                BinaryPrimitives.ReadSingleLittleEndian(bin.AsSpan(o, FloatByteSize)),
                BinaryPrimitives.ReadSingleLittleEndian(bin.AsSpan(o + FloatByteSize, FloatByteSize)),
                BinaryPrimitives.ReadSingleLittleEndian(bin.AsSpan(o + (2 * FloatByteSize), FloatByteSize)));
        }

        return result;
    }

    private static Vector4[] ReadVec4(GltfDocument doc, byte[] bin, int accessorIdx)
    {
        var (offset, count) = ResolveAccessor(doc, accessorIdx, "VEC4", ComponentTypeFloat);
        ValidateAccessorByteRange(offset, count, Vec4ByteSize, bin);
        var result = new Vector4[count];
        for (var i = 0; i < count; i++)
        {
            var o = offset + (i * Vec4ByteSize);
            result[i] = new Vector4(
                BinaryPrimitives.ReadSingleLittleEndian(bin.AsSpan(o, FloatByteSize)),
                BinaryPrimitives.ReadSingleLittleEndian(bin.AsSpan(o + FloatByteSize, FloatByteSize)),
                BinaryPrimitives.ReadSingleLittleEndian(bin.AsSpan(o + (2 * FloatByteSize), FloatByteSize)),
                BinaryPrimitives.ReadSingleLittleEndian(bin.AsSpan(o + (3 * FloatByteSize), FloatByteSize)));
        }

        return result;
    }

    private static Vector2[] ReadVec2(GltfDocument doc, byte[] bin, int accessorIdx)
    {
        var (offset, count) = ResolveAccessor(doc, accessorIdx, "VEC2", ComponentTypeFloat);
        ValidateAccessorByteRange(offset, count, Vec2ByteSize, bin);
        var result = new Vector2[count];
        for (var i = 0; i < count; i++)
        {
            var o = offset + (i * Vec2ByteSize);
            result[i] = new Vector2(
                BinaryPrimitives.ReadSingleLittleEndian(bin.AsSpan(o, FloatByteSize)),
                BinaryPrimitives.ReadSingleLittleEndian(bin.AsSpan(o + FloatByteSize, FloatByteSize)));
        }

        return result;
    }

    private static uint[] ReadIndices(GltfDocument doc, byte[] bin, int accessorIdx)
    {
        if (doc.Accessors is null || accessorIdx >= doc.Accessors.Count)
        {
            throw new InvalidDataException($"Index accessor {accessorIdx} out of range.");
        }

        var accessor = doc.Accessors[accessorIdx];
        if (accessor.Type != "SCALAR")
        {
            throw new InvalidDataException($"Index accessor type {accessor.Type} (expected SCALAR).");
        }

        var (offset, count) = ResolveAccessorOffset(doc, accessor);
        var componentByteSize = IndexComponentByteSize(accessor.ComponentType);
        ValidateAccessorByteRange(offset, count, componentByteSize, bin);

        var indices = new uint[count];
        switch (accessor.ComponentType)
        {
            case ComponentTypeUshort:
                for (var i = 0; i < count; i++)
                {
                    indices[i] = BinaryPrimitives.ReadUInt16LittleEndian(bin.AsSpan(offset + (i * UshortByteSize), UshortByteSize));
                }

                break;
            case ComponentTypeUint:
                for (var i = 0; i < count; i++)
                {
                    indices[i] = BinaryPrimitives.ReadUInt32LittleEndian(bin.AsSpan(offset + (i * UintByteSize), UintByteSize));
                }

                break;
            default:
                for (var i = 0; i < count; i++)
                {
                    indices[i] = bin[offset + i];
                }

                break;
        }

        return indices;
    }

    private static int IndexComponentByteSize(int componentType) => componentType switch
    {
        ComponentTypeUshort => UshortByteSize,
        ComponentTypeUint => UintByteSize,
        ComponentTypeUbyte => UbyteByteSize,
        _ => throw new InvalidDataException($"Unsupported index component type {componentType}."),
    };

    /// <summary>Proves an accessor's element range fits inside the GLB binary buffer before any
    /// array is allocated. The element count and byte offset originate in the glTF JSON, which is
    /// untrusted content: without this guard a hostile <c>accessors[i].count</c> drives an unbounded
    /// pre-allocation (multi-gigabyte <c>new T[count]</c>) ahead of the per-element span bounds
    /// checks, an OOM on the asset-load path. The range is computed in <see cref="long"/> so a
    /// hostile offset/count cannot overflow the check itself.</summary>
    private static void ValidateAccessorByteRange(int offset, int count, int componentByteSize, byte[] bin)
    {
        if (count < 0)
        {
            throw new InvalidDataException($"Accessor element count {count} is negative.");
        }

        if (offset < 0)
        {
            throw new InvalidDataException($"Accessor byte offset {offset} is negative.");
        }

        var requiredEnd = (long)offset + ((long)count * componentByteSize);
        if (requiredEnd > bin.Length)
        {
            throw new InvalidDataException(
                $"Accessor reads {count} elements of {componentByteSize} bytes at offset {offset}, past the {bin.Length}-byte buffer.");
        }
    }

    private static (int Offset, int Count) ResolveAccessor(GltfDocument doc, int accessorIdx, string expectedType, int expectedComponentType)
    {
        if (doc.Accessors is null || accessorIdx >= doc.Accessors.Count)
        {
            throw new InvalidDataException($"Accessor {accessorIdx} out of range.");
        }

        var accessor = doc.Accessors[accessorIdx];
        if (accessor.Type != expectedType)
        {
            throw new InvalidDataException($"Accessor {accessorIdx} type {accessor.Type} (expected {expectedType}).");
        }

        if (accessor.ComponentType != expectedComponentType)
        {
            throw new InvalidDataException($"Accessor {accessorIdx} component type {accessor.ComponentType} (expected {expectedComponentType}).");
        }

        return ResolveAccessorOffset(doc, accessor);
    }

    private static (int Offset, int Count) ResolveAccessorOffset(GltfDocument doc, GltfAccessor accessor)
    {
        if (doc.BufferViews is null || accessor.BufferView >= doc.BufferViews.Count)
        {
            throw new InvalidDataException($"BufferView {accessor.BufferView} out of range.");
        }

        var view = doc.BufferViews[accessor.BufferView];
        return (view.ByteOffset + accessor.ByteOffset, accessor.Count);
    }
}
