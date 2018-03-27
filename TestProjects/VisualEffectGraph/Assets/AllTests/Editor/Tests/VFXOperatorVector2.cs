using System;
using UnityEngine;

namespace UnityEditor.VFX.Test
{
    class VFXOperatorVector2 : VFXOperator
    {
        public class OutputProperties
        {
            public Vector2 o;
        }

        // HACK FIX
        private static VFXExpression defaultValue = null;
        private VFXExpression GetDefault
        {
            get
            {
                if (defaultValue == null)
                    defaultValue = VFXValue.Constant(new Vector2(1.0f, 2.0f));
                return defaultValue;
            }
        }

        override public string name { get { return "Temp_Vector2"; } }
        override protected VFXExpression[] BuildExpression(VFXExpression[] inputExpression)
        {
            return new[] { GetDefault };
        }
    }
}
