using System;

namespace RagdollLib.Core.Math
{
    /// <summary>
    /// Lightweight, engine-independent 3D vector used by <c>RagdollLib.Core</c> for deterministic geometry and impulse math.
    /// </summary>
    public readonly struct Vec3 : IEquatable<Vec3>
    {
        /// <summary>Zero vector (0, 0, 0).</summary>
        public static readonly Vec3 Zero = new Vec3(0f, 0f, 0f);

        /// <summary>Unit up vector (0, 1, 0).</summary>
        public static readonly Vec3 Up = new Vec3(0f, 1f, 0f);

        /// <summary>Unit forward vector (0, 0, 1).</summary>
        public static readonly Vec3 Forward = new Vec3(0f, 0f, 1f);

        /// <summary>X component.</summary>
        public float X { get; }

        /// <summary>Y component.</summary>
        public float Y { get; }

        /// <summary>Z component.</summary>
        public float Z { get; }

        /// <summary>
        /// Creates a new <see cref="Vec3"/>.
        /// </summary>
        public Vec3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>Squared Euclidean length.</summary>
        public float SqrMagnitude => (X * X) + (Y * Y) + (Z * Z);

        /// <summary>Euclidean length.</summary>
        public float Magnitude => (float)System.Math.Sqrt(SqrMagnitude);

        /// <summary>Returns a unit-length vector, or <see cref="Zero"/> if length is negligible.</summary>
        public Vec3 Normalized
        {
            get
            {
                float mag = Magnitude;
                return mag > 1e-6f ? new Vec3(X / mag, Y / mag, Z / mag) : Zero;
            }
        }

        /// <summary>Returns true if all components are finite numbers.</summary>
        public bool IsFinite =>
            !float.IsNaN(X) && !float.IsInfinity(X) &&
            !float.IsNaN(Y) && !float.IsInfinity(Y) &&
            !float.IsNaN(Z) && !float.IsInfinity(Z);

        /// <summary>Computes the Euclidean distance between two points.</summary>
        public static float Distance(Vec3 a, Vec3 b) => (a - b).Magnitude;

        /// <summary>Computes the dot product of two vectors.</summary>
        public static float Dot(Vec3 a, Vec3 b) => (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);

        /// <summary>Linearly interpolates between <paramref name="a"/> and <paramref name="b"/>.</summary>
        public static Vec3 Lerp(Vec3 a, Vec3 b, float t)
        {
            if (t <= 0f) return a;
            if (t >= 1f) return b;
            return new Vec3(a.X + ((b.X - a.X) * t), a.Y + ((b.Y - a.Y) * t), a.Z + ((b.Z - a.Z) * t));
        }

        /// <summary>Clamps vector magnitude to at most <paramref name="maxLength"/>.</summary>
        public static Vec3 ClampMagnitude(Vec3 vector, float maxLength)
        {
            if (!vector.IsFinite || maxLength <= 0f)
                return Zero;
            float sqr = vector.SqrMagnitude;
            if (sqr > maxLength * maxLength)
            {
                float scale = maxLength / (float)System.Math.Sqrt(sqr);
                return new Vec3(vector.X * scale, vector.Y * scale, vector.Z * scale);
            }
            return vector;
        }

        /// <summary>Component-wise addition.</summary>
        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        /// <summary>Component-wise subtraction.</summary>
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        /// <summary>Unary negation.</summary>
        public static Vec3 operator -(Vec3 a) => new Vec3(-a.X, -a.Y, -a.Z);

        /// <summary>Scalar multiplication.</summary>
        public static Vec3 operator *(Vec3 a, float s) => new Vec3(a.X * s, a.Y * s, a.Z * s);

        /// <summary>Scalar multiplication.</summary>
        public static Vec3 operator *(float s, Vec3 a) => new Vec3(a.X * s, a.Y * s, a.Z * s);

        /// <summary>Scalar division.</summary>
        public static Vec3 operator /(Vec3 a, float s) => new Vec3(a.X / s, a.Y / s, a.Z / s);

        /// <inheritdoc />
        public bool Equals(Vec3 other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is Vec3 other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = X.GetHashCode();
                hash = (hash * 397) ^ Y.GetHashCode();
                hash = (hash * 397) ^ Z.GetHashCode();
                return hash;
            }
        }

        /// <summary>Equality operator.</summary>
        public static bool operator ==(Vec3 left, Vec3 right) => left.Equals(right);

        /// <summary>Inequality operator.</summary>
        public static bool operator !=(Vec3 left, Vec3 right) => !left.Equals(right);

        /// <inheritdoc />
        public override string ToString() => $"({X:F3}, {Y:F3}, {Z:F3})";
    }
}
