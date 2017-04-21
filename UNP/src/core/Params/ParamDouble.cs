﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UNP.Core.Params {

    public class ParamDouble : ParamDoubleBase, iParam {

        private double value = 0.0;
        private Parameters.Units unit = Parameters.Units.ValueOrSamples;

        public ParamDouble(String name, String group, Parameters parentSet, String desc, String[] options) : base(name, group, parentSet, desc, options) { }

        public String getValue() {
            return this.value.ToString(Parameters.NumberCulture) + (this.unit == Parameters.Units.Seconds ? "s" : "");
        }

        public T getValue<T>() {

            Type paramType = typeof(T);
            if(paramType == typeof(double)) {     
                // request to return as double

                // return value
                return (T)Convert.ChangeType(Value, typeof(double));

            } else {
                // request to return as other

                // message and return 0
                logger.Error("Could not retrieve the value for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') as '" + paramType.Name + "', can only return value as double. Returning 0.0");
                return (T)Convert.ChangeType(0, typeof(T));    

            }
            
        }

        public int getValueInSamples() {

            // retrieve the value as double
            double val = getValue<double>();
            int intSamples = 0;

            // check if the unit is set in seconds
            if (unit == Parameters.Units.Seconds) {
                // flagged as seconds

                // convert, check rounding
                double samples = val * (double)MainThread.SamplesPerSecond();
                intSamples = (int)Math.Round(samples);
                if (samples != intSamples) {

                    // message
                    logger.Warn("Value for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') was retrieved in number of samples (" + val + " * " + MainThread.SamplesPerSecond() + "), but has been rounded from " + samples + " to " + intSamples);

                }

            } else {
                // not flagged as seconds
                
                // convert double to int, check rounding
                intSamples = (int)Math.Round(val);
                if (val != intSamples) {

                    // message
                    logger.Warn("Value for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') was retrieved in number of samples, but has been rounded from " + val + " to " + intSamples);

                }

            }

            // return number of samples
            return intSamples;

        }

        public override string ToString() {
            return getValue();
        }

        public double Value {
            get {   return this.value;  }
        }
        
        public Parameters.Units Unit {
            get {   return this.unit;  }
        }
        
        public bool setValue(double value) {
            this.value = value;
            return true;
        }

        public bool tryValue(String value) {
            double doubleValue;
            Parameters.Units unit;
            return tryParseValue(value, out doubleValue, out unit);
        }

        public bool setValue(String value) {

            // try to parse the value
            double doubleValue;
            Parameters.Units unit;
            if (!tryParseValue(value, out doubleValue, out unit)) {

                // message
                logger.Error("Could not store the value for parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "'), value '" + value + "' could not be parsed as double");
                
                // return failure
                return false;

            }
            
            // check if the parameter has options and the unit is set in seconds
            if (this.options.Length > 0 && unit == Parameters.Units.Seconds) {

                // message
                logger.Warn("Parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') has a list of options, yet the value is specified in seconds, this is unlikely to be correct");
                    
            }

            // assign
            this.value = doubleValue;
            this.unit = unit;

            // return success
            return true;

        }

        public bool setUnit(Parameters.Units unit) {

            // check if the parameter has options and the unit is set in seconds
            if (this.options.Length > 0 && unit == Parameters.Units.Seconds) {

                // message
                logger.Warn("Parameter '" + this.Name + "' (parameter set: '" + this.getParentSetName() + "') has a list of options, yet the value set to be in seconds, this is unlikely to be correct");
                    
            }

            // set units
            this.unit = unit;

            // return success
            return true;

        }
        
    }

}