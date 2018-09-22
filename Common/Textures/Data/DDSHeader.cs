﻿using System;
using System.Runtime.InteropServices;

namespace Common.Textures.Data
{
    [StructLayout(LayoutKind.Sequential)]
    public struct PixelFormat
    {
        public int Size;
        public int Flags;
        public int FourCC;
        public int RGBBitCount;
        public int RBitMask;
        public int GBitMask;
        public int BBitMask;
        public int AlphaBitMask;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DDSCaps
    {
        public int Caps1;
        public int Caps2;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public int[] Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DDSHeader
    {
        public int Magic;
        public int Size;
        public int Flags;
        public int Height;
        public int Width;
        public int PitchOrLinearSize;
        public int Depth;
        public int MipMapCount;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 11)]
        public int[] Reserved1;

        public PixelFormat PixelFormat;
        public DDSCaps DDSCaps;

        public int Reserved2;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="texture"></param>
        public void Init(Texture texture)
        {
            this.Magic = 0x20534444; // "DDS "
            this.Size = 0x7C;
            this.Flags = 0;

            // Set flags
            this.Flags |= 0x1; // DDSD_CAPS
            this.Flags |= 0x2; // DDSD_HEIGHT
            this.Flags |= 0x4; // DDSD_WIDTH
            this.Flags |= 0x1000; // DDSD_PIXELFORMAT
            this.Flags |= 0x20000; // DDSD_MIPMAPCOUNT

            this.Height = (int)texture.Height;
            this.Width = (int)texture.Width;
            this.PitchOrLinearSize = texture.Data.Length;
            this.Depth = 1;
            this.MipMapCount = (int)texture.MipMapCount;

            this.PixelFormat.Size = 32;
            this.PixelFormat.Flags = 0;

            if (texture.CompressionType == TextureCompression.Ati1
                || texture.CompressionType == TextureCompression.Ati2
                || texture.CompressionType == TextureCompression.Dxt1
                || texture.CompressionType == TextureCompression.Dxt3
                || texture.CompressionType == TextureCompression.Dxt5)
            {
                this.Flags |= 0x80000; // DDSD_LINEARSIZE
                this.DDSCaps.Caps1 = 0x401008;
                this.PixelFormat.Flags |= 0x4;

                switch (texture.CompressionType)
                {
                    case TextureCompression.Dxt1:
                    case TextureCompression.Dxt3:
                    case TextureCompression.Dxt5:
                        this.PixelFormat.FourCC = (int)texture.CompressionType;
                        break;
                    case TextureCompression.Ati1:
                        {
                            this.PixelFormat.FourCC = 0x55344342;
                            break;
                        }
                    case TextureCompression.Ati2:
                        {
                            this.PixelFormat.FourCC = 0x55354342;
                            break;
                        }
                    default: break;
                }
            }
            else if (texture.CompressionType == TextureCompression.A8R8G8B8
                     || texture.CompressionType == TextureCompression.P8)
            {
                this.Flags |= 0x8; // DDSD_PITCH
                this.PitchOrLinearSize = (int)texture.PitchOrLinearSize;
                this.PixelFormat.Flags |= 0x40;
                this.PixelFormat.Flags |= 0x1;

                this.PixelFormat.RGBBitCount = 0x20;
                this.PixelFormat.RBitMask = 0xFF0000;
                this.PixelFormat.GBitMask = 0xFF00;
                this.PixelFormat.BBitMask = 0xFF;
                this.PixelFormat.AlphaBitMask = unchecked((int)0xFF000000);
                this.DDSCaps.Caps1 = 0x40100A;
            }
            else
            {
                throw new Exception("What did you just give me?");
            }
        }
    }
}
