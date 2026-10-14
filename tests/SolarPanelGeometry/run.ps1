# Runs the production solar geometry methods outside Unity. Only KSP state and
# vector/rotation primitives are stubbed; the methods under test are extracted
# from SolarPanelFixer.cs so the regression checks exercise the current source.
$ErrorActionPreference = 'Stop'
$repoPath = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$solarSource = Get-Content -Raw (Join-Path $repoPath 'src/Kerbalism/Modules/SolarPanelFixer.cs')

function Get-ProductionMethod([string] $signature) {
    $start = $solarSource.IndexOf($signature, [StringComparison]::Ordinal)
    if ($start -lt 0) { throw "Missing production method: $signature" }
    $body = $solarSource.IndexOf('{', $start)
    $depth = 1
    $end = $body + 1
    while ($depth -gt 0) {
        if ($solarSource[$end] -eq '{') { $depth++ }
        if ($solarSource[$end] -eq '}') { $depth-- }
        $end++
    }
    return $solarSource.Substring($start, $end - $start)
}

$signatures = @(
    'public static double TrackingCosineFactor(',
    'public static bool TryGetTrackingFaceNormal(',
    'static double GetAnalyticFlatCosineFactor(Vector3d',
    'static double GetAnalyticFlatCosineFactor(VesselData.SunInfo',
    'static Vector3d GetSunDirectionAtBodyRotationSample(',
    'static double CalculateLandedAnalyticFlatCosineDuty(',
    'static double GetTheoreticalMaxCosineFactor(',
    'static bool TryCalculateSubStepPanelPower(',
    'static double CalculateTheoreticalMaxPower(',
    'static double CalculateAvailableMaxPower(',
    'static double CalculateMaxPower(',
    'static bool TryGetFrozenPowerFactor(',
    'public static double CalculateLandedMultiStarPower(Vessel v, List<VesselData.SunInfo> suns, VesselData.SunInfo mainSun, ModuleDeployableSolarPanel.PanelType panelType, bool isTracking, out',
    'private static double CalculateSurfaceDaylightDuty('
)
$methods = ($signatures | ForEach-Object { Get-ProductionMethod $_ }) -join "`n"
$sampleCount = [regex]::Match($solarSource, 'private const int LandedOrientationSampleCount = \d+;').Value
$intervalSource = Get-Content -Raw (Join-Path $repoPath 'src/Kerbalism/Simulation/SubSteps/SubStepData.cs')
$intervalSource = [regex]::Replace($intervalSource, '(?m)^using [^\r\n]+;\s*', '')
$stubs = @'
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
namespace KERBALISM {
public struct Vector3d {
    public double x,y,z;
    public Vector3d(double x,double y,double z) { this.x=x;this.y=y;this.z=z; }
    public static Vector3d zero => new Vector3d();
    public static Vector3d up => new Vector3d(0,1,0);
    public double sqrMagnitude => Dot(this,this);
    public Vector3d normalized => sqrMagnitude == 0 ? zero : this / Math.Sqrt(sqrMagnitude);
    public void Normalize() { this=normalized; }
    public static double Dot(Vector3d a,Vector3d b) => a.x*b.x+a.y*b.y+a.z*b.z;
    public static Vector3d Cross(Vector3d a,Vector3d b) => new Vector3d(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);
    public static Vector3d operator +(Vector3d a,Vector3d b) => new Vector3d(a.x+b.x,a.y+b.y,a.z+b.z);
    public static Vector3d operator -(Vector3d a,Vector3d b) => new Vector3d(a.x-b.x,a.y-b.y,a.z-b.z);
    public static Vector3d operator *(Vector3d a,double b) => new Vector3d(a.x*b,a.y*b,a.z*b);
    public static Vector3d operator /(Vector3d a,double b) => a*(1/b);
}
public struct QuaternionD {
    double angle; Vector3d axis;
    public QuaternionD swizzle => this;
    public static QuaternionD AngleAxis(double degrees,Vector3d axis) => new QuaternionD { angle=degrees*Math.PI/180,axis=axis.normalized };
    public static Vector3d operator *(QuaternionD q,Vector3d v) => v*Math.Cos(q.angle)+Vector3d.Cross(q.axis,v)*Math.Sin(q.angle)+q.axis*(Vector3d.Dot(q.axis,v)*(1-Math.Cos(q.angle)));
}
public class Transform { public Vector3d up=Vector3d.up; }
public class Frame { public QuaternionD Rotation; }
public class CelestialBody {
    public bool tidallyLocked; public CelestialBody referenceBody;
    public Vector3d position; public Transform transform=new Transform();
    public Frame BodyFrame=new Frame(); public double rotPeriodRecip,angularV;
}
public class Vessel {
    public Guid id=Guid.NewGuid(); public CelestialBody mainBody=new CelestialBody();
    public Vector3d Position=new Vector3d(1,0,0); public bool Landed=true;
    public VesselData Data=new VesselData(); public VesselData KerbalismData() => Data;
}
public class VesselData {
    public bool EnvIsAnalytic=true; public SubStepIntervalResult SubStepInterval;
    public List<SunInfo> EnvSunsInfo;
    public class SunInfo {
        public Sim.SunData SunData; public Vector3d Direction;
        public double Distance=1,AtmoFactor=1,SolarFlux=500;
        public SubStepIntervalResult Interval;
    }
}
public static class Sim {
    public static double SolarFluxAtHome=1000;
    public class SunData { public CelestialBody body=new CelestialBody(); public int bodyIndex; public double SolarFlux(double distance) => 1000; }
}
public static class Lib {
    public static Vector3d VesselPosition(Vessel v) => v.Position;
    public static bool Landed(Vessel v) => v.Landed;
    public static double Clamp(double x,double min,double max) => Math.Max(min,Math.Min(max,x));
}
public class PreferencesGeneral {
    public static PreferencesGeneral Instance=new PreferencesGeneral();
    public bool freezeUnloadedSolarPanelExposure;
}
public class ModuleDeployableSolarPanel { public enum PanelType { FLAT,SPHERICAL,CYLINDRICAL } }
}
'@
$tests = @'
public static string RunTests() {
    var v=new Vessel();
    var sun=new VesselData.SunInfo { SunData=new Sim.SunData(),Direction=new Vector3d(1,0,0) };
    var suns=new List<VesselData.SunInfo> { sun }; v.Data.EnvSunsInfo=suns;
    var up=new Vector3d(1,0,0); var down=new Vector3d(-1,0,0);
    double ideal;
    AssertNear("fallback upward static panel",CalculateLandedMultiStarPower(v,suns,sun,ModuleDeployableSolarPanel.PanelType.FLAT,false,out ideal,true,Vector3d.zero,up),1/Math.PI,0.001);
    AssertNear("fallback downward static panel",CalculateLandedMultiStarPower(v,suns,sun,ModuleDeployableSolarPanel.PanelType.FLAT,false,out ideal,true,Vector3d.zero,down),0);
    AssertNear("fallback north-axis tracker",CalculateLandedMultiStarPower(v,suns,sun,ModuleDeployableSolarPanel.PanelType.FLAT,true,out ideal,true,Vector3d.up,up),0.5);
    AssertNear("fallback spherical daylight",CalculateLandedMultiStarPower(v,suns,sun,ModuleDeployableSolarPanel.PanelType.SPHERICAL,false,out ideal),0.125);
    AssertNear("fallback cylindrical daylight",CalculateLandedMultiStarPower(v,suns,sun,ModuleDeployableSolarPanel.PanelType.CYLINDRICAL,false,out ideal),0.5/Math.PI);
    v.mainBody.tidallyLocked=true; v.mainBody.referenceBody=sun.SunData.body;
    AssertNear("sun-locked daylight",CalculateLandedMultiStarPower(v,suns,sun,ModuleDeployableSolarPanel.PanelType.FLAT,false,out ideal,true,Vector3d.zero,up),1);
    sun.Direction=down;
    AssertNear("sun-locked night",CalculateLandedMultiStarPower(v,suns,sun,ModuleDeployableSolarPanel.PanelType.FLAT,false,out ideal,true,Vector3d.zero,up),0);
    v.mainBody.tidallyLocked=false; v.mainBody.rotPeriodRecip=-1.0/200; v.mainBody.angularV=Math.PI/100;
    sun.Direction=new Vector3d(0,0,1);
    var geometry=new[] { new SubStepGeometrySample(25,50,up,0,0,0),new SubStepGeometrySample(75,50,up,0,0,0) };
    var star=new SubStepSunResult { Samples=Array.AsReadOnly(new[] {
        new SubStepSunSample(25,50,sun.Direction,1,true,1,1000),
        new SubStepSunSample(75,50,sun.Direction,1,true,1,1000) }) };
    var interval=new SubStepIntervalResult(v.id,0,100,1,geometry,new Dictionary<int,SubStepSunResult> {{0,star}},null);
    v.Data.SubStepInterval=interval; sun.Interval=interval;
    double actual;
    if (!TryCalculateSubStepPanelPower(v,suns,sun,ModuleDeployableSolarPanel.PanelType.FLAT,false,true,Vector3d.zero,up,out actual,out ideal)) throw new Exception("valid interval rejected");
    AssertNear("retrograde landed panel",actual,Math.Sqrt(0.5));
    AssertNear("sampled theoretical power",ideal,1);
    v.Landed=false; PreferencesGeneral.Instance.freezeUnloadedSolarPanelExposure=true;
    double telemetry;
    if (!TryGetFrozenPowerFactor(v,true,0.8,v.Data,ModuleDeployableSolarPanel.PanelType.FLAT,out actual,out ideal,out telemetry)) throw new Exception("frozen exposure rejected");
    AssertNear("frozen output retains shadow duty",actual,0.4);
    AssertNear("frozen telemetry retains exposure",telemetry/ideal,0.8);
    v.Landed=true;
    if (TryGetFrozenPowerFactor(v,true,0.8,v.Data,ModuleDeployableSolarPanel.PanelType.FLAT,out actual,out ideal,out telemetry)) throw new Exception("landed exposure was frozen");
    return "PASS: 12 solar fallback, retrograde rotation and frozen-exposure checks";
}
static void AssertNear(string name,double actual,double expected,double tolerance=1e-10) {
    if (double.IsNaN(actual) || Math.Abs(actual-expected)>tolerance) throw new Exception(name+": expected "+expected+", got "+actual);
}
'@
Add-Type -TypeDefinition ($stubs + $intervalSource + "`nnamespace KERBALISM { public static class SolarGeometryRegression {`n" + $sampleCount + "`n" + $methods + "`n" + $tests + "`n} }")
[KERBALISM.SolarGeometryRegression]::RunTests()
