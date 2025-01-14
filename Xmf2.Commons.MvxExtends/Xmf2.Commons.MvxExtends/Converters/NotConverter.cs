﻿using System;
using MvvmCross.Platform.Converters;

namespace Xmf2.Commons.MvxExtends.Converters
{
	public class NotConverter : MvxValueConverter<bool, bool>
	{
		protected override bool Convert(bool value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			return !value;
		}
	}
}

