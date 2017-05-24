using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebimSDK.Extensions
{
    internal static class StringExtensions
    {
        internal static bool ExistsAndEquals(string leftValue, string rightValue)
        {
            return !string.IsNullOrEmpty(leftValue) && leftValue.Equals(rightValue);
        }
    }
}
