﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Common.Geometry.Data;

namespace Common.Geometry
{
    public class ProStreetSolids : SolidListManager
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct SolidListInfo
        {
            public long Blank;

            public int Marker; // this doesn't change between games for some reason... rather unfortunate

            public int NumObjects;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x38)]
            public string PipelinePath;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x20)]
            public string ClassType;

            public int UnknownOffset, UnknownSize;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct SolidObjectHeader
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
            public byte[] Blank;

            public uint Unknown1;

            public uint Hash;

            public uint NumTris;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] Unknown2;

            public uint Blank2;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] BoundsMin;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public float[] BoundsMax;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public float[] Transform;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public uint[] Unknown3;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
            public uint[] Unknown4;
        }

        /// <remarks>
        /// for speedyheart
        /// </remarks>
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct TestTrackShadingGroup
        {
            // begin header
            public uint FirstIndex;

            public uint Unknown1;

            public uint Blank;

            public uint Unknown2;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public uint[] Unknown3;

            // end header

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public byte[] TextureShaderUsage;

            public uint Unknown4;

            public uint UnknownId;
            public uint Flags;
            public ushort IndicesUsed;
            public ushort Unknown5;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
            public uint[] Blank2;

            public uint VertexBufferUsage;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public readonly byte[] Flags2;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
            public uint[] Blank3;

            public uint Unknown6;
            public uint Dummy1;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct SolidObjectShadingGroup
        {
            // begin header
            public uint FirstIndex;

            public uint Unknown1;

            public uint Blank;

            public uint Unknown2;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public uint[] Unknown3;

            // end header

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public byte[] TextureShaderUsage;

            public uint Unknown4;

            public uint UnknownId;
            public uint Flags;
            public uint IndicesUsed;
            public uint Unknown5;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
            public uint[] Blank2;

            public uint VertexBufferUsage; // / 0x20
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public readonly byte[] Flags2;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
            public uint[] Blank3;

            public uint Unknown6;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct SolidObjectDescriptor
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public uint[] Blank1;

            public uint Unknown1;

            public uint Flags;

            public uint MaterialShaderCount;

            public uint Blank2;

            public uint NumVertexStreams; // should be 0, real count == MaterialShaderCount

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public uint[] Blank3;

            public uint NumTris; // 0 if NumTriIndices

            public uint NumTriIndices; // 0 if NumTris

            public uint Unknown2;

            public uint Blank4;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 0x18)]
        private struct SolidObjectOffset
        {
            public uint ObjectHash;
            public uint Offset;
            public uint CompressedSize;
            public uint OutSize;
            public uint Unknown1;
            public uint Unknown2;
        }

        private const uint SolidListInfoChunk = 0x134002;
        private const uint SolidListObjHeadChunk = 0x134011;

        private SolidList _solidList;

        private int _namedMaterials;

        private readonly bool _testTrackMode; // for speedyheart

        public ProStreetSolids(bool testTrackMode = false)
        {
            _testTrackMode = testTrackMode;
        }

        public override SolidList ReadSolidList(BinaryReader br, uint containerSize)
        {
            _solidList = new SolidList();
            _namedMaterials = 0;

            ReadChunks(br, containerSize);

            return _solidList;
        }

        protected override void ReadChunks(BinaryReader br, uint containerSize)
        {
            var endPos = br.BaseStream.Position + containerSize;

            while (br.BaseStream.Position < endPos)
            {
                var chunkId = br.ReadUInt32();
                var chunkSize = br.ReadUInt32();
                var chunkEndPos = br.BaseStream.Position + chunkSize;

                if (chunkId == 0x55441122)
                {
                    break;
                }

                if ((chunkId & 0x80000000) == 0x80000000 && chunkId != 0x80134010)
                {
                    ReadChunks(br, chunkSize);
                }
                else
                {
                    if ((chunkId & 0x80000000) != 0x80000000)
                    {
                        var padding = 0u;

                        while (br.BaseStream.Position < chunkEndPos && br.ReadUInt32() == 0x11111111)
                        {
                            padding += 4;

                            if (br.BaseStream.Position >= chunkEndPos)
                            {
                                break;
                            }
                        }

                        br.BaseStream.Position -= 4;
                        chunkSize -= padding;
                    }

                    switch (chunkId)
                    {
                        case SolidListInfoChunk:
                            {
                                var info = BinaryUtil.ReadStruct<SolidListInfo>(br);

                                _solidList.ClassType = info.ClassType;
                                _solidList.PipelinePath = info.PipelinePath;
                                _solidList.ObjectCount = info.NumObjects;

                                break;
                            }
                        case 0x134004:
                            {
                                while (br.BaseStream.Position < chunkEndPos)
                                {
                                    var och = BinaryUtil.ReadStruct<SolidObjectOffset>(br);
                                    var curPos = br.BaseStream.Position;
                                    br.BaseStream.Position = och.Offset;

                                    var bytesRead = 0u;
                                    var blocks = new List<byte[]>();

                                    while (bytesRead < och.CompressedSize)
                                    {
                                        var compHeader = BinaryUtil.ReadStruct<Compression.CompressBlockHead>(br);
                                        var compressedData = br.ReadBytes((int)(compHeader.TotalBlockSize - 24));
                                        var outData = new byte[compHeader.OutSize];

                                        Compression.Decompress(compressedData, outData);

                                        blocks.Add(outData);

                                        bytesRead += compHeader.TotalBlockSize;
                                    }

                                    if (blocks.Count == 1)
                                    {
                                        using (var ms = new MemoryStream(blocks[0]))
                                        using (var mbr = new BinaryReader(ms))
                                        {
                                            var solidObject = ReadObject(mbr, blocks[0].Length, true, null);
                                            solidObject.PostProcessing();

                                            _solidList.Objects.Add(solidObject);
                                        }
                                    }
                                    else if (blocks.Count > 1)
                                    {
                                        // Sort the blocks into their proper order.
                                        var sorted = new List<byte>();

                                        sorted.AddRange(blocks[blocks.Count - 1]);

                                        for (var j = 0; j < blocks.Count; j++)
                                        {
                                            if (j != blocks.Count - 1)
                                            {
                                                sorted.AddRange(blocks[j]);
                                            }
                                        }

                                        using (var ms = new MemoryStream(sorted.ToArray()))
                                        using (var mbr = new BinaryReader(ms))
                                        {
                                            var solidObject = ReadObject(mbr, sorted.Count, true, null);
                                            solidObject.PostProcessing();

                                            _solidList.Objects.Add(solidObject);
                                        }

                                        sorted.Clear();
                                    }

                                    blocks.Clear();

                                    br.BaseStream.Position = curPos;
                                }
                                break;
                            }
                        case 0x80134010:
                            {
                                var solidObject = ReadObject(br, chunkSize, false, null);
                                solidObject.PostProcessing();
                                _solidList.Objects.Add(solidObject);
                                break;
                            }
                        default:
                            //Console.WriteLine($"0x{chunkId:X8} [{chunkSize}] @{br.BaseStream.Position}");
                            break;
                    }
                }

                br.BaseStream.Position = chunkEndPos;
            }
        }

        public override void WriteSolidList(ChunkStream chunkStream, SolidList solidList)
        {
            throw new NotImplementedException();
        }

        private SolidObject ReadObject(BinaryReader br, long size, bool compressed, SolidObject solidObject)
        {
            if (solidObject == null)
                solidObject = new ProStreetObject(_testTrackMode);

            solidObject.IsCompressed = compressed;
            solidObject.EnableTransform = !compressed;

            if (_testTrackMode)
            {
                solidObject.RotationAngle = 0.0f;
            }

            var endPos = br.BaseStream.Position + size;

            while (br.BaseStream.Position < endPos)
            {
                var chunkId = br.ReadUInt32();
                var chunkSize = br.ReadUInt32();
                var chunkEndPos = br.BaseStream.Position + chunkSize;

                if ((chunkId & 0x80000000) == 0x80000000)
                {
                    solidObject = ReadObject(br, chunkSize, compressed, solidObject);
                }
                else
                {
                    var padding = 0u;

                    while (br.BaseStream.Position < chunkEndPos && br.ReadUInt32() == 0x11111111)
                    {
                        padding += 4;

                        if (br.BaseStream.Position >= chunkEndPos)
                        {
                            break;
                        }
                    }

                    br.BaseStream.Position -= 4;
                    chunkSize -= padding;

                    switch (chunkId)
                    {
                        case SolidListObjHeadChunk:
                            {
                                _namedMaterials = 0;

                                var header = BinaryUtil.ReadStruct<SolidObjectHeader>(br);
                                var name = BinaryUtil.ReadNullTerminatedString(br);

                                solidObject.Name = name;
                                solidObject.Hash = header.Hash;
                                solidObject.MinPoint = new SimpleVector4(
                                    header.BoundsMin[0],
                                    header.BoundsMin[1],
                                    header.BoundsMin[2],
                                    header.BoundsMin[3]
                                );

                                solidObject.MaxPoint = new SimpleVector4(
                                    header.BoundsMax[0],
                                    header.BoundsMax[1],
                                    header.BoundsMax[2],
                                    header.BoundsMax[3]
                                );

                                solidObject.NumTris = header.NumTris;
                                solidObject.NumShaders = 0;
                                solidObject.NumTextures = 0;
                                solidObject.Transform = new SimpleMatrix
                                {
                                    Data = new[,]
                                    {
                                        { header.Transform[0], header.Transform[1], header.Transform[2], header.Transform[3]},
                                        { header.Transform[4], header.Transform[5], header.Transform[6], header.Transform[7]},
                                        { header.Transform[8], header.Transform[9], header.Transform[10], header.Transform[11]},
                                        { header.Transform[12], header.Transform[13], header.Transform[14], header.Transform[15]}
                                    }
                                };

                                break;
                            }
                        // 12 40 13 00
                        case 0x00134012:
                            {
                                for (var j = 0; j < chunkSize / 8; j++)
                                {
                                    var val = br.ReadUInt32();

                                    if (val != 0)
                                    {
                                        solidObject.TextureHashes.Add(val);
                                    }

                                    br.BaseStream.Position += 4;
                                }

                                break;
                            }
                        case 0x134900:
                            {
                                var descriptor = BinaryUtil.ReadStruct<SolidObjectDescriptor>(br);

                                solidObject.MeshDescriptor = new SolidMeshDescriptor
                                {
                                    Flags = descriptor.Flags,
                                    HasNormals = true,
                                    NumIndices = descriptor.NumTriIndices,
                                    NumMats = descriptor.MaterialShaderCount,
                                    NumVertexStreams = descriptor.NumVertexStreams
                                };

                                break;
                            }
                        case 0x00134b02:
                            {
                                if (_testTrackMode)
                                {
                                    var shadingGroupSize = Marshal.SizeOf<TestTrackShadingGroup>();
                                    Debug.Assert(chunkSize % shadingGroupSize == 0);
                                    var numMats = chunkSize / shadingGroupSize;

                                    for (var j = 0; j < numMats; j++)
                                    {
                                        var shadingGroup = br.GetStruct<TestTrackShadingGroup>();

                                        solidObject.Materials.Add(new ProStreetMaterial
                                        {
                                            Flags = shadingGroup.Flags,
                                            NumIndices = shadingGroup.IndicesUsed,
                                            NumTris = (uint) (shadingGroup.IndicesUsed / 3),
                                            Name = $"Unnamed Material #{j + 1:00}",
                                            NumVerts = shadingGroup.VertexBufferUsage / shadingGroup.Flags2[2],
                                            VertexStreamIndex = j,
                                            Hash = shadingGroup.UnknownId,
                                            TextureHash = solidObject.TextureHashes[shadingGroup.TextureShaderUsage[4]]
                                        });

                                        solidObject.MeshDescriptor.NumVerts +=
                                            shadingGroup.VertexBufferUsage / shadingGroup.Flags2[2];
                                    }
                                }
                                else
                                {
                                    var shadingGroupSize = Marshal.SizeOf<SolidObjectShadingGroup>();
                                    Debug.Assert(chunkSize % shadingGroupSize == 0);
                                    var numMats = chunkSize / shadingGroupSize;

                                    for (var j = 0; j < numMats; j++)
                                    {
                                        var shadingGroup = br.GetStruct<SolidObjectShadingGroup>();

                                        solidObject.Materials.Add(new ProStreetMaterial
                                        {
                                            Flags = shadingGroup.Flags,
                                            NumIndices = shadingGroup.IndicesUsed,
                                            NumTris = shadingGroup.IndicesUsed / 3,
                                            Name = $"Unnamed Material #{j + 1:00}",
                                            NumVerts = shadingGroup.VertexBufferUsage / shadingGroup.Flags2[2],
                                            VertexStreamIndex = j,
                                            Hash = shadingGroup.UnknownId,
                                            TextureHash = solidObject.TextureHashes[shadingGroup.TextureShaderUsage[4]]
                                        });

                                        solidObject.MeshDescriptor.NumVerts +=
                                            shadingGroup.VertexBufferUsage / shadingGroup.Flags2[2];
                                    }
                                }

                                break;
                            }
                        case 0x134b01:
                            {
                                var vb = new VertexBuffer
                                {
                                    Data = new float[chunkSize >> 2]
                                };

                                var pos = 0;

                                while (br.BaseStream.Position < chunkEndPos)
                                {
                                    var v = br.ReadSingle();

                                    vb.Data[pos++] = v;
                                }

                                solidObject.VertexBuffers.Add(vb);
                                break;
                            }
                        case 0x134b03:
                            {
                                Array.Resize(ref solidObject.Faces, (int)solidObject.Materials.Sum(m => m.NumTris));

                                var faceIndex = 0;

                                foreach (var material in solidObject.Materials)
                                {
                                    for (var j = 0; j < material.NumTris; j++)
                                    {
                                        var f1 = br.ReadUInt16();
                                        var f2 = br.ReadUInt16();
                                        var f3 = br.ReadUInt16();

                                        solidObject.Faces[faceIndex++] = new SolidMeshFace
                                        {
                                            Vtx1 = f1,
                                            Vtx2 = f2,
                                            Vtx3 = f3
                                        };
                                    }
                                }

                                break;
                            }
                        case 0x00134c02:
                            {
                                if (chunkSize > 0)
                                {
                                    solidObject.Materials[_namedMaterials++].Name =
                                        BinaryUtil.ReadNullTerminatedString(br);
                                }
                                break;
                            }
                        default:
                            //Console.WriteLine($"0x{chunkId:X8} [{chunkSize}] @{br.BaseStream.Position}");
                            break;
                    }
                }

                br.BaseStream.Position = chunkEndPos;
            }

            return solidObject;
        }
    }
}
