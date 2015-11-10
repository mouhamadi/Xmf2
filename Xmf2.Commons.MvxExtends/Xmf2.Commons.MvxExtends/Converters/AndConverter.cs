﻿using Cirrious.CrossCore.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Xmf2.Commons.MvxExtends.Converters
{
    public class AndConverter : MvxValueConverter<bool, bool>
    {
        protected override bool Convert(bool value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value && (bool)parameter;
        }
    }
}