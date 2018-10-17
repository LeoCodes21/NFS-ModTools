﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Common.Geometry.Data;

namespace Common.Geometry
{
    public class Underground2Solids : SolidListManager
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct SolidListInfo
        {
            public readonly long Blank;

            public readonly int Marker; // this doesn't change between games for some reason... rather unfortunate

            public readonly int NumObjects;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x38)]
            public readonly string PipelinePath;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x20)]
            public readonly string ClassType;

            public readonly int UnknownOffset;
            public readonly int UnknownSize;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct SolidObjectHeader
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
            public readonly byte[] Blank;

            public readonly uint Unknown1;

            public readonly uint Hash;

            public readonly uint NumTris;

            public readonly byte Blank2;
            public readonly byte TextureCount;
            public readonly byte ShaderCount;
            public readonly byte Blank3;

            public readonly int Blank4;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public readonly float[] BoundsMin;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public readonly float[] BoundsMax;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public readonly float[] Transform;

            public readonly long Blank5;
            public readonly int Unknown2;
            public readonly int Unknown3;
            public readonly int Blank6;
            public float Unknown4;
            public float Unknown5;
            public int Unknown6;
            public int Unknown7;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct SolidObjectShadingGroup
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public readonly float[] BoundsMin;

            public readonly uint Length; // indices

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public readonly float[] BoundsMax;

            public readonly uint TextureIndex;
            public readonly uint ShaderIndex;
            public readonly uint D1, D2, D3, D4;
            public readonly uint Offset; // indices
            public readonly uint Flags;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct SolidObjectDescriptor
        {
            public readonly long Blank1;
            public readonly int Unknown1;
            public readonly uint Flags;
            public readonly uint NumMats;
            public readonly long Blank2;
            public readonly long Blank3;
            public readonly long Blank4;
            public readonly long Blank5;
            public readonly uint NumTris;
            public readonly uint Blank6;
            public readonly uint Blank7;
            public readonly uint Blank8;
            public readonly uint NumVerts;
            public readonly uint Blank9;
            public readonly uint Blank10;
            public readonly uint Blank11;
        }

        private const uint SolidListInfoChunk = 0x134002;
        private const uint SolidListObjHeadChunk = 0x134011;

        private SolidList _solidList;

        private int _namedMaterials;

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


                if ((chunkId & 0x80000000) == 0x80000000 && chunkId != 0x80134010)
                {
                    ReadChunks(br, chunkSize);
                }
                else
                {
                    //if ((chunkId & 0x80000000) != 0x80000000)
                    //{
                    //    var padding = 0u;

                    //    while (br.ReadUInt32() == 0x11111111)
                    //    {
                    //        padding += 4;

                    //        if (br.BaseStream.Position >= chunkEndPos)
                    //        {
                    //            break;
                    //        }
                    //    }

                    //    br.BaseStream.Position -= 4;
                    //    chunkSize -= padding;
                    //}

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
                        case 0x80134010:
                            {
                                var solidObject = ReadObject(br, chunkSize, null);
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

        private SolidObject ReadObject(BinaryReader br, long size, SolidObject solidObject)
        {
            if (solidObject == null)
                solidObject = new Underground2Object();

            var endPos = br.BaseStream.Position + size;

            while (br.BaseStream.Position < endPos)
            {
                var chunkId = br.ReadUInt32();
                var chunkSize = br.ReadUInt32();
                var chunkEndPos = br.BaseStream.Position + chunkSize;

                if ((chunkId & 0x80000000) == 0x80000000)
                {
                    solidObject = ReadObject(br, chunkSize, solidObject);
                }
                else
                {
                    var padding = 0u;

                    while (br.ReadUInt32() == 0x11111111)
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
                                var name = new string(br.ReadChars(0x1C)).Trim('\0');

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
                                solidObject.NumShaders = header.ShaderCount;
                                solidObject.NumTextures = header.TextureCount;
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
                                    solidObject.TextureHashes.Add(br.ReadUInt32());
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
                                    HasNormals = (descriptor.Flags & 0x0080) != 0,
                                    NumIndices = descriptor.NumTris * 3,
                                    NumMats = descriptor.NumMats,
                                    NumVertexStreams = 1
                                };

                                break;
                            }
                        case 0x00134b02:
                            {
                                const int shadingGroupSize = 60;
                                Debug.Assert(chunkSize % shadingGroupSize == 0);
                                var numMats = chunkSize / shadingGroupSize;

                                for (var j = 0; j < numMats; j++)
                                {
                                    var shadingGroup = br.GetStruct<SolidObjectShadingGroup>();

                                    solidObject.Materials.Add(new Underground2Material
                                    {
                                        Name = $"Unnamed Material #{j + 1:00}",
                                        Flags = shadingGroup.Flags,
                                        MinPoint = new SimpleVector3(shadingGroup.BoundsMin[0], shadingGroup.BoundsMin[1], shadingGroup.BoundsMin[2]),
                                        MaxPoint = new SimpleVector3(shadingGroup.BoundsMax[0], shadingGroup.BoundsMax[1], shadingGroup.BoundsMax[2]),
                                        NumIndices = shadingGroup.Length,
                                        NumTris = shadingGroup.Length / 3,
                                        VertexStreamIndex = 0,
                                        TextureHash = solidObject.TextureHashes[(int) shadingGroup.TextureIndex],
                                        TextureIndex = (byte) shadingGroup.TextureIndex,
                                        RawStructure = shadingGroup
                                    });
                                }

                                break;
                            }
                        case 0x134b01:
                            {
                                var vb = new VertexBuffer
                                {
                                    Data = new float[chunkSize >> 2]
                                };

                                if (chunkSize > 0)
                                {
                                    var pos = 0;

                                    while (br.BaseStream.Position < chunkEndPos)
                                    {
                                        var v = br.ReadSingle();

                                        vb.Data[pos++] = v;
                                    }
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
