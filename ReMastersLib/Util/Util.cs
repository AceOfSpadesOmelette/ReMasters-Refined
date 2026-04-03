using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;

namespace ReMastersLib
{
    public static class Util
    {
        public static byte[] GetSlice(byte[] src, int offset, int length)
        {
            byte[] data = new byte[length];
            Buffer.BlockCopy(src, offset, data, 0, data.Length);
            return data;
        }

        public static void Copy(string source, string target, SearchOption so = SearchOption.AllDirectories)
        {
            var diSource = new DirectoryInfo(source);
            var diTarget = new DirectoryInfo(target);
            
            CopyAll(diSource, diTarget, so);
        }
        public static void CopyAll(DirectoryInfo source, DirectoryInfo target, SearchOption so = SearchOption.AllDirectories)
        {
            Directory.CreateDirectory(target.FullName);
            
            foreach (FileInfo fi in source.GetFiles("*", SearchOption.TopDirectoryOnly))
            {
                fi.CopyTo(Path.Combine(target.FullName, fi.Name), true);
            }

            if (so == SearchOption.AllDirectories)
            {
                foreach (DirectoryInfo subDirectory in source.GetDirectories())
                {
                    DirectoryInfo nextTargetSubDir = target.CreateSubdirectory(subDirectory.Name);
                    CopyAll(subDirectory, nextTargetSubDir);
                }
            }

        }
    }
}