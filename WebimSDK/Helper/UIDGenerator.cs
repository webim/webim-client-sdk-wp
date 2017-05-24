using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebimSDK
{
    internal sealed class UIDGenerator
    {
        private static readonly UIDGenerator instance = new UIDGenerator();
        private Random _Random;

        private UIDGenerator()
        {
            _Random = new Random();
        }

        public static UIDGenerator Instance
        {
            get
            {
                return instance;
            }
        }

        public static string Next()
        {
            return (-Instance._Random.Next()).ToString();
        }

        public static string NextPositive()
        {
            return (Instance._Random.Next()).ToString();
        }
    }
}
