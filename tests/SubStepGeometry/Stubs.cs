using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

// Only host state and buffer/scheduling APIs are replaced. All geometry and
// validation code comes from the production files; Mathematics is the real DLL.
namespace Unity.Burst { public class BurstCompileAttribute : Attribute { } }
namespace Unity.Jobs { public interface IJobParallelFor { void Execute(int index); } }
namespace Unity.Collections.LowLevel.Unsafe { public class NativeDisableParallelForRestrictionAttribute : Attribute { } }
namespace Unity.Collections
{
    public class ReadOnlyAttribute : Attribute { }
    public class WriteOnlyAttribute : Attribute { }
    public enum Allocator { Persistent }
    public enum NativeArrayOptions { UninitializedMemory }
    public struct NativeArray<T> where T : struct
    {
        T[] values;
        public NativeArray(int length, Allocator allocator, NativeArrayOptions options) { values = new T[length]; }
        public bool IsCreated => values != null;
        public int Length => values.Length;
        public T this[int index] { get => values[index]; set => values[index] = value; }
        public void Dispose() { values = null; }
    }
}
namespace KERBALISM
{
    public struct Vector3d
    {
        public double x, y, z;
        public Vector3d(double x, double y, double z) { this.x = x; this.y = y; this.z = z; }
        public Vector3d normalized { get { double r = Math.Sqrt(x*x+y*y+z*z); return new Vector3d(x/r,y/r,z/r); } }
        public Vector3d xzy => new Vector3d(x,z,y);
        public static Vector3d operator -(Vector3d a, Vector3d b) => new Vector3d(a.x-b.x,a.y-b.y,a.z-b.z);
    }
    public class Frame
    {
        public Vector3d X = new Vector3d(1,0,0), Y = new Vector3d(0,0,1);
        public Vector3d WorldToLocal(Vector3d value) => value;
    }
    public static class Planetarium { public static Frame Zup = new Frame(); }
    public class Orbit
    {
        public CelestialBody referenceBody;
        public double eccentricity, meanMotion = 1, semiLatusRectum = 2, epoch, ObTAtEpoch, period = 2*Math.PI;
        public double inclination, StartUT, EndUT = double.PositiveInfinity;
        public Frame OrbitFrame = new Frame();
    }
    public class OrbitDriver { public Orbit orbit; }
    public class CelestialBody
    {
        public OrbitDriver orbitDriver;
        public int flightGlobalsIndex;
        public double Radius = 1, atmosphereDepth, rotPeriodRecip, angularV;
        public Vector3d position;
        public Vector3d GetWorldSurfacePosition(double latitude, double longitude, double altitude)
        {
            double lat = latitude*Math.PI/180, lon = longitude*Math.PI/180, r = Radius+altitude;
            return new Vector3d(position.x+r*Math.Cos(lat)*Math.Cos(lon), position.y+r*Math.Sin(lat), position.z+r*Math.Cos(lat)*Math.Sin(lon));
        }
    }
    public class Vessel
    {
        public CelestialBody mainBody;
        public OrbitDriver orbitDriver;
        public bool Landed;
        public double latitude, longitude, altitude;
    }
    public static class Lib { public static bool Landed(Vessel vessel) => vessel.Landed; }
}
