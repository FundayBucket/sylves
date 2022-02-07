﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Sylves
{
    /// <summary>
    /// Represents rotations / reflections of a square
    /// </summary>
    public struct SquareRotation
    {
        private static readonly SquareRotation[] all =
        {
            new SquareRotation(0),
            new SquareRotation(1),
            new SquareRotation(2),
            new SquareRotation(3),
            new SquareRotation(~0),
            new SquareRotation(~1),
            new SquareRotation(~2),
            new SquareRotation(~3),
        };

        short value;

        private SquareRotation(short value)
        {
            this.value = value;
        }

        public bool IsReflection => value < 0;

        public int Rotation => value < 0 ? ~value : value;

        public static SquareRotation Identity => new SquareRotation(0);

        public static SquareRotation ReflectX => new SquareRotation(~2);

        public static SquareRotation ReflectY => new SquareRotation(~0);

        public static SquareRotation RotateCCW => new SquareRotation(1);

        public static SquareRotation RotateCW => new SquareRotation(-1);

        public static SquareRotation Rotate90(int i) => new SquareRotation((short)(((i % 4) + 4) % 4));

        public static SquareRotation[] All => all;

        public SquareRotation Invert()
        {
            if (IsReflection)
            {
                return this;
            }
            else
            {
                return new SquareRotation((short)((4 - value) % 4));
            }
        }

        public override bool Equals(object obj)
        {
            return obj is SquareRotation rotation &&
                   value == rotation.value;
        }

        public override int GetHashCode()
        {
            return 45106587 + value.GetHashCode();
        }

        public static bool operator ==(SquareRotation a, SquareRotation b)
        {
            return a.value == b.value;
        }

        public static bool operator !=(SquareRotation a, SquareRotation b)
        {
            return a.value != b.value;
        }

        public static SquareRotation operator *(SquareRotation a, SquareRotation b)
        {
            var isReflection = a.IsReflection ^ b.IsReflection;
            var rotation = a * (b * 0);
            return new SquareRotation(isReflection ? (short)~rotation : (short)rotation);
        }

        public static SquareDir operator *(SquareRotation rotation, SquareDir dir)
        {
            var side = (int)(dir);
            var newSide = (rotation.IsReflection ? rotation.Rotation - side + 4 : rotation.Rotation + side) % 4;
            return (SquareDir)(newSide);
        }

        public static Vector3Int operator *(SquareRotation r, Vector3Int v)
        {
            switch (r.value)
            {
                case 0: break;
                case 1:
                    (v.x, v.y) = (-v.y, v.x);
                    break;
                case 2:
                    (v.x, v.y) = (-v.x, -v.y);
                    break;
                case 3:
                    (v.x, v.y) = (v.y, -v.x);
                    break;
                case ~0:
                    v.y = -v.y;
                    break;
                case ~1:
                    (v.x, v.y) = (v.y, v.x);
                    break;
                case ~2:
                    v.x = -v.x;
                    break;
                case ~3:
                    (v.x, v.y) = (-v.y, -v.x);
                    break;
            }
            return v;
        }

        public static implicit operator SquareRotation(CellRotation r) => new SquareRotation((short)r);

        public static implicit operator CellRotation(SquareRotation r) => (CellRotation)r.value;
    }
}
