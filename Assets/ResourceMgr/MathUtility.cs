using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ResourceMoudle
{
    public class MathUtility
    {
        public static float CalculateProgress(float start, float scale, int i, int imax)
        {
            if (imax == 0)
            {
                return start + scale;
            }

            return start + (((float)i / imax) * scale);
        }
    }
}