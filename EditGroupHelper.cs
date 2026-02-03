using System;
using AppGroup.View;

namespace AppGroup {
    public class EditGroupHelper {
        private readonly string windowTitle;
        private readonly int groupId;

        public EditGroupHelper(string windowTitle, int groupId) {
            this.windowTitle = windowTitle;
            this.groupId = groupId;
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
    }
}
