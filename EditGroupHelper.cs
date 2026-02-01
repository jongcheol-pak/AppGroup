using Microsoft.UI.Windowing;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Text;
using Microsoft.UI;
using Windows.UI.WindowManagement;
using Microsoft.UI.Xaml;
using AppGroup.View;

namespace AppGroup {
    public class EditGroupHelper {
        private readonly string windowTitle;
        private readonly int groupId;
        private readonly string groupIdFilePath;
        private readonly string logFilePath;

        

        public EditGroupHelper(string windowTitle, int groupId) {
            this.windowTitle = windowTitle;
            this.groupId = groupId;
            // Define the local application data path
            //string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            //string appDataPath = Path.Combine(localAppDataPath, "AppGroup");

            //// Ensure the directory exists
            //if (!Directory.Exists(appDataPath)) {
            //    Directory.CreateDirectory(appDataPath);
            //}

            //groupIdFilePath = Path.Combine(appDataPath, "gid");


        }

        public bool IsExist() {
            IntPtr hWnd = NativeMethods.FindWindow(null, windowTitle);
            return hWnd != IntPtr.Zero;
        }

        public void Activate() {
            IntPtr hWnd = NativeMethods.FindWindow(null, windowTitle);
            if (hWnd != IntPtr.Zero) {
                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
                NativeMethods.SetForegroundWindow(hWnd);
            }
            else {
                var editWindow = new EditGroupWindow(groupId);
                editWindow.Activate();
            }
        }

        private bool UpdateFile() {

            try {
                File.WriteAllText(groupIdFilePath, groupId.ToString());

                return true;
            }
            catch (Exception ex) {
                Debug.WriteLine($"Direct file update failed: {ex.Message}");

                return false;
            }
        }



    }
}
