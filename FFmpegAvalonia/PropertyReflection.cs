﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FFmpegAvalonia
{
    internal class PropertyReflection
    {
        public string GetPropVal(string propertyName)
        {
            object? val = this.GetType().GetProperty(propertyName).GetValue(this);
            if (val is bool bVal)
            {
                return bVal.ToString();
            }
            else if (val is null)
            {
                return "null";
            }
            else
            {
                return (string)val;
            }
        }
    }
}
