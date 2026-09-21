using System;
using System.IO;

namespace Crispberry_PiPhone
{
    /// <summary>Moves deleted phone media into <c>CrispberryPiPhone/trash</c>.</summary>
    internal static class PhoneTrash
    {
        public static string TrashDir
        {
            get { return Path.Combine(PhoneStore.RootDir, "trash"); }
        }

        public static bool Recycle(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;
            bool file = false;
            bool dir = false;
            try
            {
                file = File.Exists(path);
                dir = !file && Directory.Exists(path);
            }
            catch
            {
            }
            if (!file && !dir)
                return false;
            try
            {
                Directory.CreateDirectory(TrashDir);
                string dest = UniquePath(Path.GetFileName(path));
                if (file)
                    File.Move(path, dest);
                else
                    Directory.Move(path, dest);
                return true;
            }
            catch
            {
            }
            try
            {
                if (file)
                {
                    File.Delete(path);
                    return true;
                }
                if (dir)
                {
                    Directory.Delete(path, true);
                    return true;
                }
            }
            catch
            {
            }
            return false;
        }

        public static void Reveal()
        {
            PhoneStore.OpenFolder(TrashDir);
        }

        private static string UniquePath(string name)
        {
            if (string.IsNullOrEmpty(name))
                name = "item";
            string dest = Path.Combine(TrashDir, name);
            if (!File.Exists(dest) && !Directory.Exists(dest))
                return dest;
            string stem = Path.GetFileNameWithoutExtension(name);
            string ext = Path.GetExtension(name);
            string stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            dest = Path.Combine(TrashDir, stem + "_" + stamp + ext);
            int n = 2;
            while (File.Exists(dest) || Directory.Exists(dest))
            {
                dest = Path.Combine(TrashDir, stem + "_" + stamp + "_" + n + ext);
                n++;
            }
            return dest;
        }
    }
}
