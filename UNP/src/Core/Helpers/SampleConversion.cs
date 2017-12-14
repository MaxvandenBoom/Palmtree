﻿/**
 * The SampleConversion class
 * 
 * ...
 * 
 * 
 * Copyright (C) 2017:  RIBS group (Nick Ramsey Lab), University Medical Center Utrecht (The Netherlands) & external contributors
 * Author(s):           Max van den Boom            (info@maxvandenboom.nl)
 * 
 * This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software
 * Foundation, either version 3 of the License, or (at your option) any later version. This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for
 * more details. You should have received a copy of the GNU General Public License along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using System;

namespace UNP.Core.Helpers {

    /// <summary>
    /// The <c>SampleConversion</c> class.
    /// 
    /// ...
    /// </summary>
    public static class SampleConversion {

        public static double sampleRate() {
            return MainThread.getPipelineSamplesPerSecond();
        }

        public static int timeToSamples(double timeInSeconds) {
            return (int)Math.Round(timeInSeconds * MainThread.getPipelineSamplesPerSecond());
        }

        public static double timeToSamplesAsDouble(double timeInSeconds) {
            return timeInSeconds * MainThread.getPipelineSamplesPerSecond();
        }

        public static double samplesToTime(int samples) {
            return (double)samples / MainThread.getPipelineSamplesPerSecond();
        }
    }

}
