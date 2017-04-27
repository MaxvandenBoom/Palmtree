﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UNP.Core.Params {

    public abstract class ParamBoolBase : Param {
        protected bool boolStdValue = false;

        public ParamBoolBase(string name, string group, Parameters parentSet, string desc, string[] options) : base(name, group, parentSet, desc, options) { }

        public bool setStdValue(string stdValue) {

            // make lowercase and store the standardvalue
            this.stdValue = stdValue.ToLower();

            // interpret the standard value 
            this.boolStdValue = (this.stdValue.Equals("1") || this.stdValue.Equals("true"));

            // return true
            return true;

        }

    }

}
