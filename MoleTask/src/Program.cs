﻿using System;
using System.IO;
using System.Runtime.InteropServices;
using UNP;
using UNP.applications;

namespace MoleTask {


    class Program {

        static void Main(string[] args) {

            // check if all dependencies exists
            


            // TODO: Add startup arguments (args)
            // - nogui = start without GUI
            // - parameter file =
            // - autosetconfig = 
            // - autostart = 
            
            Type t = Type.GetType("MoleTask.MoleTask");
            MainBoot.Run(t);

        }

    }
}
