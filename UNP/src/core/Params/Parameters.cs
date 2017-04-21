﻿using NLog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UNP.Core.Helpers;

namespace UNP.Core.Params {

    public class Parameters {

        public static Char[] ArrDelimiters = new Char[] { ' ', ',', ';', '\n' };

        public static Char[] MatColumnDelimiters = new Char[] { ';', '\n' };
        public static Char[] MatRowDelimiters = new Char[] { ',' };
        public static CultureInfo NumberCulture = CultureInfo.CreateSpecificCulture("en-US");
        private static Logger logger = LogManager.GetLogger("Parameters");

        private List<iParam> paramList = new List<iParam>(0);
        private String paramSetName = "";
        private ParamSetTypes paramSetType = ParamSetTypes.Source;

        public enum ParamSetTypes : int {
            Source = 0,
            Filter = 1,
            Application = 2
        }

        public enum Units : int {
            ValueOrSamples = 0,
            Seconds = 1
        }

        public Parameters(String paramSetName, ParamSetTypes paramSetType) {
            this.paramSetName = paramSetName;
            this.paramSetType = paramSetType;
        }

        public String ParamSetName {
            get { return this.paramSetName; }
        }
        
        public ParamSetTypes ParamSetType {
            get { return this.paramSetType; }
        }

        public List<iParam> getParameters() {
            return paramList;
        }

        public iParam addParameter<T>(String name, String desc) {
            return addParameter<T>(name, "", desc, "", "", "", new String[0]);
        }        
        public iParam addParameter<T>(String name, String desc, String stdValue) {
            return addParameter<T>(name, "", desc, "", "", stdValue, new String[0]);
        }
        public iParam addParameter<T>(String name, String desc, String[] options) {
            return addParameter<T>(name, "", desc, "", "", "", options);
        }
        public iParam addParameter<T>(String name, String desc, String minValue, String maxValue, String stdValue) {
            return addParameter<T>(name, "", desc, minValue, maxValue, stdValue, new String[0]);
        }
        public iParam addParameter<T>(String name, String desc, String minValue, String maxValue, String[] options) {
            return addParameter<T>(name, "", desc, minValue, maxValue, "", options);
        }
        public iParam addParameter<T>(String name, String desc, String minValue, String maxValue, String stdValue, String[] options) {
            return addParameter<T>(name, "", desc, minValue, maxValue, stdValue, options);
        }
        public iParam addParameter<T>(String name, String group, String desc, String minValue, String maxValue, String stdValue) {
            return addParameter<T>(name, group, desc, minValue, maxValue, stdValue, new String[0]);
        }
        public iParam addParameter<T>(String name, String group, String desc, String minValue, String maxValue, String stdValue, String[] options) {

            // check if a parameter with that name already exists, return without adding if this is the case
            if (getParameter(name) != null) {
                logger.Warn("A parameter with the name '" + name + "' already exists in parameter set '" + paramSetName + "', not added.");
                return null;
            }

            // create a new parameter and transfer the properties
            iParam param = null;
            Type paramType = typeof(T);
            if(paramType == typeof(ParamBool) || paramType == typeof(bool) || paramType == typeof(Boolean)) {
                
                param = new ParamBool(name, group, this, desc, options);

            } else if (paramType == typeof(ParamInt) || paramType == typeof(int)) {

                param = new ParamInt(name, group, this, desc, options);

            } else if (paramType == typeof(ParamDouble) || paramType == typeof(double)) {

                param = new ParamDouble(name, group, this, desc, options);

            } else if (paramType == typeof(ParamBoolArr) || paramType == typeof(bool[]) || paramType == typeof(Boolean[])) {

                param = new ParamBoolArr(name, group, this, desc, options);
                
            } else if (paramType == typeof(ParamIntArr) || paramType == typeof(int[])) {

                param = new ParamIntArr(name, group, this, desc, options);

            } else if (paramType == typeof(ParamDoubleArr) || paramType == typeof(double[])) {

                param = new ParamDoubleArr(name, group, this, desc, options);

            } else if (paramType == typeof(ParamBoolMat) || paramType == typeof(bool[][]) || paramType == typeof(Boolean[][])) {

                param = new ParamBoolMat(name, group, this, desc, options);
                
            } else if (paramType == typeof(ParamIntMat) || paramType == typeof(int[][])) {

                param = new ParamIntMat(name, group, this, desc, options);

            } else if (paramType == typeof(ParamDoubleMat) || paramType == typeof(double[][])) {

                param = new ParamDoubleMat(name, group, this, desc, options);

            } else if (paramType == typeof(ParamColor) || paramType == typeof(RGBColorFloat)) {

                param = new ParamColor(name, group, this, desc, options);
                                       
            } else {
                
                // message
                logger.Error("Unknown parameter (generic) type '" + paramType.Name + "' for parameter name '" + name + "' in parameter set '" + paramSetName + "', not added.");

                // return without adding parameter
                return null;
            }

            // check if the parameter is integer based
            if(param is ParamIntBase) {

                // check if the minimum value is valid
                if (!((ParamIntBase)param).setMinValue(minValue)) {

                    // message
                    logger.Error("Could not add parameter '" + name + "' in parameter set '" + paramSetName + "', minimum value '" + minValue + "' could not be parsed as an integer");

                    // return without adding parameter
                    return null;

                }

                // check if the maximum value is valid
                if (!((ParamIntBase)param).setMaxValue(maxValue)) {

                    // message
                    logger.Error("Could not add parameter '" + name + "' in parameter set '" + paramSetName + "', maximum value '" + maxValue + "' could not be parsed as an integer");

                    // return without adding parameter
                    return null;

                }

            }

            // check if the parameter is double based
            if (param is ParamDoubleBase) {

                // check if the minimum value is valid
                if (!((ParamDoubleBase)param).setMinValue(minValue)) {

                    // message
                    logger.Error("Could not add parameter '" + name + "' in parameter set '" + paramSetName + "', minimum value '" + minValue + "' could not be parsed as an integer");

                    // return without adding parameter
                    return null;

                }

                // check if the maximum value is valid
                if (!((ParamDoubleBase)param).setMaxValue(maxValue)) {

                    // message
                    logger.Error("Could not add parameter '" + name + "' in parameter set '" + paramSetName + "', maximum value '" + maxValue + "' could not be parsed as an integer");

                    // return without adding parameter
                    return null;

                }

            }

            // set the standard value
            if (param.setStdValue(stdValue)) {
                // succesfully set standard value

                // For parameter types which hold just one value, the standard
                // value can be set initially to this value

                // check if the parameter is of the type that just holds a single value
                if (param is ParamBool || param is ParamInt || param is ParamDouble || param is ParamColor) {
                    
                    // set the standard value as initial value
                    param.setValue(param.StdValue);

                }

            } else {
                // failed to set standard value

                // message
                logger.Error("Could not add parameter '" + name + "' in parameter set '" + paramSetName + "', standard value '" + stdValue + "' is empty or could not be parsed");

                // return without adding parameter
                return null;

            }

            // add the parameter to the list
            paramList.Add(param);
            return param;

        }

        private iParam getParameter(String paramName) {

            // try to find the parameter by name
            for (int i = 0; i < paramList.Count(); i++) {
                if (paramList[i].Name.Equals(paramName)) {
                    return paramList[i];
                }
            }

            // return
            return null;

        }

        public T getValue<T>(String paramName) {
            iParam param = getParameter(paramName);
            if (param == null) {

                // message
                logger.Error("Could not find parameter '" + paramName + "' in parameter set '" + paramSetName + "', returning 0");

                // return 0
                return (T)Convert.ChangeType(0, typeof(T));

            }
            
            // return the value
            return param.getValue<T>();

        }
        
        public int getValueInSamples(String paramName) {
            iParam param = getParameter(paramName);
            if (param == null) {

                // message
                logger.Error("Could not find parameter '" + paramName + "' in parameter set '" + paramSetName + "', returning 0");

                // return 0
                return 0;

            }
            
            // return the value
            return param.getValueInSamples();

        }

        public bool setValue(String paramName, bool paramValue) {
            iParam param = getParameter(paramName);
            if (param == null) {
                logger.Error("Could not find parameter '" + paramName + "' in parameter set '" + paramSetName + "', value not set");
                return false;
            }

            // check if the parameter is indeed used to store a boolean
            if (param.GetType() != typeof(ParamBool)) {
                logger.Error("Could not set parameter '" + paramName + "' in parameter set '" + paramSetName + "', trying to set a boolean value in a '" + param.GetType().Name + "' parameter");
                return false;
            }

            // set the value
            if (!((ParamBool)param).setValue(paramValue))   return false;

            // return success
            return true;

        }

        public bool setValue(String paramName, int paramValue) {
            return setValue(paramName, paramValue, Units.ValueOrSamples);
        }
        public bool setValue(String paramName, int paramValue, Parameters.Units paramUnit) {
            iParam param = getParameter(paramName);
            if (param == null) {
                logger.Error("Could not find parameter '" + paramName + "' in parameter set '" + paramSetName + "', value not set");
                return false;
            }

            // check if the parameter is indeed used to store an integer
            if (param.GetType() != typeof(ParamInt)) {
                logger.Error("Could not set parameter '" + paramName + "' in parameter set '" + paramSetName + "', trying to set an integer value in a '" + param.GetType().Name + "' parameter");
                return false;
            }

            // set the value
            if (!((ParamInt)param).setValue(paramValue))    return false;
            if (!((ParamInt)param).setUnit(paramUnit))      return false;

            // return success
            return true;

        }

        public bool setValue(String paramName, double paramValue) {
            return setValue(paramName, paramValue, Units.ValueOrSamples);
        }
        public bool setValue(String paramName, double paramValue, Parameters.Units paramUnit) {
            iParam param = getParameter(paramName);
            if (param == null) {
                logger.Error("Could not find parameter '" + paramName + "' in parameter set '" + paramSetName + "', value not set");
                return false;
            }

            // check if the parameter is indeed used to store a double
            if (param.GetType() != typeof(ParamDouble)) {
                logger.Error("Could not set parameter '" + paramName + "' in parameter set '" + paramSetName + "', trying to set a double value in a '" + param.GetType().Name + "' parameter");
                return false;
            }

            // set the value
            if (!((ParamDouble)param).setValue(paramValue))     return false;
            if (!((ParamDouble)param).setUnit(paramUnit))       return false;

            // return success
            return true;

        }

        public bool setValue(String paramName, bool[] paramValue) {
            iParam param = getParameter(paramName);
            if (param == null) {
                logger.Error("Could not find parameter '" + paramName + "' in parameter set '" + paramSetName + "', value not set");
                return false;
            }

            // check if the parameter is indeed used to store a boolean array
            if (param.GetType() != typeof(ParamBoolArr)) {
                logger.Error("Could not set parameter '" + paramName + "' in parameter set '" + paramSetName + "', trying to set an array of booleans in a '" + param.GetType().Name + "' parameter");
                return false;
            }

            // set the value
            if (!((ParamBoolArr)param).setValue(paramValue))     return false;

            // return success
            return true;

        }

        public bool setValue(String paramName, int[] paramValue) {
            return setValue(paramName, paramValue, new Parameters.Units[paramValue.Length]);
        }
        public bool setValue(String paramName, int[] paramValue, Parameters.Units[] paramUnit) {
            iParam param = getParameter(paramName);
            if (param == null) {
                logger.Error("Could not find parameter '" + paramName + "' in parameter set '" + paramSetName + "', value not set");
                return false;
            }

            // check if the parameter is indeed used to store an integer array
            if (param.GetType() != typeof(ParamIntArr)) {
                logger.Error("Could not set parameter '" + paramName + "' in parameter set '" + paramSetName + "', trying to set an array of integers in a '" + param.GetType().Name + "' parameter");
                return false;
            }

            // check if the parameter value 


            // set the value
            if (!((ParamIntArr)param).setValue(paramValue))     return false;
            if (!((ParamIntArr)param).setUnit(paramUnit))       return false;

            // return success
            return true;

        }

        public bool setValue(String paramName, double[] paramValue) {
            return setValue(paramName, paramValue, new Parameters.Units[paramValue.Length]);
        }
        public bool setValue(String paramName, double[] paramValue, Parameters.Units[] paramUnit) {
            iParam param = getParameter(paramName);
            if (param == null) {
                logger.Error("Could not find parameter '" + paramName + "' in parameter set '" + paramSetName + "', value not set");
                return false;
            }

            // check if the parameter is indeed used to store a double array
            if (param.GetType() != typeof(ParamDoubleArr)) {
                logger.Error("Could not set parameter '" + paramName + "' in parameter set '" + paramSetName + "', trying to set an array of doubles in a '" + param.GetType().Name + "' parameter");
                return false;
            }

            // set the value
            if (!((ParamDoubleArr)param).setValue(paramValue))      return false;
            if (!((ParamDoubleArr)param).setUnit(paramUnit))        return false;

            // return success
            return true;

        }

        public bool setValue(String paramName, bool[][] paramValue) {
            iParam param = getParameter(paramName);
            if (param == null) {
                logger.Error("Could not find parameter '" + paramName + "' in parameter set '" + paramSetName + "', value not set");
                return false;
            }

            // check if the parameter is indeed used to store a boolean matix
            if (param.GetType() != typeof(ParamBoolMat)) {
                logger.Error("Could not set parameter '" + paramName + "' in parameter set '" + paramSetName + "', trying to set a matrix of doubles in a '" + param.GetType().Name + "' parameter");
                return false;
            }

            // set the value
            if (!((ParamBoolMat)param).setValue(paramValue))      return false;

            // return success
            return true;

        }

        public bool setValue(String paramName, int[][] paramValue) {
            Parameters.Units[][] paramUnit = new Parameters.Units[paramValue.Length][];
            for (int i = 0; i < paramUnit.Count(); i++) paramUnit[i] = new Parameters.Units[paramValue[i].Length];
            return setValue(paramName, paramValue, paramUnit);
        }
        public bool setValue(String paramName, int[][] paramValue, Parameters.Units[][] paramUnit) {
            iParam param = getParameter(paramName);
            if (param == null) {
                logger.Error("Could not find parameter '" + paramName + "' in parameter set '" + paramSetName + "', value not set");
                return false;
            }

            // check if the parameter is indeed used to store a integer matix
            if (param.GetType() != typeof(ParamIntMat)) {
                logger.Error("Could not set parameter '" + paramName + "' in parameter set '" + paramSetName + "', trying to set a matrix of integers in a '" + param.GetType().Name + "' parameter");
                return false;
            }

            // set the value
            if (!((ParamIntMat)param).setValue(paramValue))      return false;
            if (!((ParamIntMat)param).setUnit(paramUnit))        return false;

            // return success
            return true;

        }

        public bool setValue(String paramName, double[][] paramValue) {
            Parameters.Units[][] paramUnit = new Parameters.Units[paramValue.Length][];
            for (int i = 0; i < paramUnit.Count(); i++) paramUnit[i] = new Parameters.Units[paramValue[i].Length];
            return setValue(paramName, paramValue, paramUnit);
        }
        public bool setValue(String paramName, double[][] paramValue, Parameters.Units[][] paramUnit) {
            iParam param = getParameter(paramName);
            if (param == null) {
                logger.Error("Could not find parameter '" + paramName + "' in parameter set '" + paramSetName + "', value not set");
                return false;
            }

            // check if the parameter is indeed used to store a double matix
            if (param.GetType() != typeof(ParamDoubleMat)) {
                logger.Error("Could not set parameter '" + paramName + "' in parameter set '" + paramSetName + "', trying to set a matrix of doubles in a '" + param.GetType().Name + "' parameter");
                return false;
            }

            // set the value
            if (!((ParamDoubleMat)param).setValue(paramValue))      return false;
            if (!((ParamDoubleMat)param).setUnit(paramUnit))        return false;

            // return success
            return true;

        }

        public bool setValue(String paramName, String paramValue) {
            iParam param = getParameter(paramName);
            if (param == null) {
                logger.Error("Could not find parameter '" + paramName + "' in parameter set '" + paramSetName + "', value not set");
                return false;
            }

            // call setter of the parameter for further processing
            if (!param.setValue(paramValue))    return false;

            // return success
            return true;

        }

    }

}