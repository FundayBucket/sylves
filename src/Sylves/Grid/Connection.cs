﻿using System;

namespace Sylves
{
    public struct Connection : IEquatable<Connection>
    {
        public bool Mirror { get; set; }
        public int Rotation { get; set; }
        public int Sides { get; set; }

        public static Connection operator*(Connection a, Connection b)
        {
            var n = a.Sides != 0 ? a.Sides : b.Sides;
            int rotation;

            if (n == 0)
            {
                rotation = 0;
            }
            else
            {
                // Same logic as NGonCellType
                if (!a.Mirror)
                {
                    rotation = ((a.Rotation + b.Rotation) % n);
                }
                else
                {
                    rotation = ((n + a.Rotation - b.Rotation) % n);
                }
            }

            return new Connection
            {
                Rotation = rotation,
                Mirror = a.Mirror ^ b.Mirror,
                Sides = n,
            };

        }

        public Connection GetInverse()
        {
            if (Rotation != 0)
                throw new NotImplementedException();
            return new Connection
            {
                Mirror = Mirror,
            };
        }

        public bool Equals(Connection other) => Mirror == other.Mirror && Rotation == other.Rotation && Sides == other.Sides;
        public override bool Equals(object other)
        {
            if (other is Connection v)
                return Equals(v);
            return false;
        }
        public override int GetHashCode() => (Mirror, Rotation, Sides).GetHashCode();

        public static bool operator ==(Connection lhs, Connection rhs) => lhs.Equals(rhs);
        public static bool operator !=(Connection lhs, Connection rhs) => !(lhs == rhs);

    }
}
