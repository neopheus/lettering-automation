﻿using System.Diagnostics;
using Lettering.Data;

namespace Lettering {
    internal class SetupManager {
        //NOTE(adam): returns true if safe to continue
        internal static bool CheckSetup(MainWindow mainWindow) {
            bool libInstall = LibraryInstaller.InstallLibrary();
            string neededFonts = FontChecker.GetNeededFonts(mainWindow);
            bool fontInstall = neededFonts.Length > 0;

            string msg = "";
            msg += (libInstall ? "Library had to be updated.\n\n" : "");
            msg += (fontInstall ? "Font(s) missing or need to be updated:\n" + neededFonts : "");
            msg += "\nCorel must be restarted before continuing.";
            msg += "\n\nPlease save all work and press OK to close Corel.";

            if(libInstall || fontInstall) {
                if(Lettering.corel.Visible) {
                    Messenger.Show(msg, "Corel Restart Required");

                    //NOTE(adam): set all documents as clean to prevent error on quit
                    foreach(VGCore.Document document in Lettering.corel.Documents) {
                        document.Dirty = false;
                    }

                    Lettering.corel.Quit();
                }

                if(fontInstall) {
                    //NOTE(adam): open font folder and display message listing needed fonts
                    Process.Start(FilePaths.networkFontsPath);
                    System.Threading.Thread.Sleep(200);     //NOTE(adam): delay to ensure dialog on top of folder window
                    Messenger.Show("Font(s) need to be installed or updated:\n" + neededFonts, "Missing Fonts");
                    return false;   //NOTE(adam): prevent continuing without fonts installed
                } else {
                    return true;
                }
            } else {
                return true;
            }
        }
    }
}
