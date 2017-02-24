using Microsoft.Win32;
using System;
using System.Runtime.InteropServices;

namespace JSONDB.ElevatedClient
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            if (args.Length <= 0) return;

            if (args[0] == "--set-association")
            {
                SetAssociation(args[1], args[2], args[3], args[4]);
            }
        }

        public static void SetAssociation(string extension, string keyName, string openWith, string fileDescription)
        {
            var baseKey = Registry.ClassesRoot.CreateSubKey(extension);
            baseKey?.SetValue("", keyName);

            var openMethod = Registry.ClassesRoot.CreateSubKey(keyName);
            openMethod?.SetValue("", fileDescription);
            openMethod?.CreateSubKey("DefaultIcon")?.SetValue("", "\"" + openWith + "\",0");

            var shell = openMethod?.CreateSubKey("Shell");
            shell?.CreateSubKey("edit")?.CreateSubKey("command")?.SetValue("", "\"" + openWith + "\"" + " \"%1\"");
            shell?.CreateSubKey("open")?.CreateSubKey("command")?.SetValue("", "\"" + openWith + "\"" + " \"%1\"");

            baseKey?.Close();
            openMethod?.Close();
            shell?.Close();

            // Delete the key instead of trying to change it
            var currentUser = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\FileExts\\" + extension, true);
            currentUser?.DeleteSubKey("UserChoice", false);

            currentUser?.Close();

            // Tell explorer the file association has been changed
            NativeMethods.SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
        }

        private static class NativeMethods
        {
            [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
        }
    }
}
