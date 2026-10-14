namespace KERBALISM
{
    public static partial class GeometryChecks
    {
        static int assertions;
        static void Check(bool result, string name)
        {
            assertions++;
            if (!result) throw new Exception(name);
        }
        static void Near(double actual, double expected, double tolerance, string name)
        {
            Check(!double.IsNaN(actual) && Math.Abs(actual-expected) <= tolerance,
                name + ": expected " + expected + ", got " + actual);
        }
        static NativeArray<T> Buffer<T>(int length) where T : struct =>
            new NativeArray<T>(length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        static SubStepOrbitNative Orbit(double eccentricity, double periapsis)
        {
            return new SubStepOrbitNative { IsValid=1, Eccentricity=eccentricity, MeanMotion=1,
                Period=eccentricity<1 ? 2*Math.PI : double.PositiveInfinity,
                SemiLatusRectum=periapsis*(1+eccentricity), BasisX=new double3(1,0,0), BasisY=new double3(0,1,0) };
        }
        static void Conics()
        {
            var solver = typeof(SubStepOrbitNative).GetMethod("SolveEccentricAnomaly", BindingFlags.NonPublic|BindingFlags.Static);
            foreach (double e in new[] {1.00000002,1.0001,1.2,2.0,5.0})
            foreach (double h in new[] {-5.0,-1.0,-0.25291891643785813,-0.001,0,0.001,0.25291891643785813,1.0,5.0})
            {
                // Construct M from a known anomaly, independently of the solver.
                double mean = e*Math.Sinh(h)-h;
                double actual = (double)solver.Invoke(null,new object[] {mean,e});
                Near(actual,h,1e-8,"hyperbolic anomaly e="+e+", H="+h);
                Check(math.all(math.isfinite(Orbit(e,6200000).PositionAtUT(mean))),"finite hyperbolic position");
            }
            double regressionE=1.0001, regressionH=0.25291891643785813;
            var position=Orbit(regressionE,6200000).PositionAtUT(regressionE*Math.Sinh(regressionH)-regressionH);
            Near(math.length(position),2000000000,1,"review regression radius");
            foreach (double e in new[] {0.0,0.5,0.9,0.9999})
            foreach (double eccentricAnomaly in new[] {-3.0,-1.0,-0.1,0,0.1,1.0,3.0})
            {
                double a=100/(1-e), mean=eccentricAnomaly-e*Math.Sin(eccentricAnomaly);
                var actual=Orbit(e,100).PositionAtUT(mean);
                Near(actual.x,a*(Math.Cos(eccentricAnomaly)-e),0.001,"elliptical x");
                Near(actual.y,a*Math.Sqrt(1-e*e)*Math.Sin(eccentricAnomaly),0.001,"elliptical y");
            }
        }
        static void RotationAndPatches()
        {
            var body=new CelestialBody {rotPeriodRecip=-1.0/200,angularV=2*Math.PI/200};
            var slots=new Dictionary<CelestialBody,int> {{body,0}};
            var snapshot=CreateBodySnapshot(body,slots,100);
            Near(snapshot.AngularVelocity,-2*Math.PI/200,1e-14,"signed snapshot angular velocity");
            SubStepJobMath.BodyBasisAtUT(snapshot,50,out var north,out var prime,out var east);
            Near(prime.z,1,1e-12,"retrograde quarter-day prime meridian");
            Near(east.x,-1,1e-12,"retrograde quarter-day east");
            var vessel=new Vessel {mainBody=body,orbitDriver=new OrbitDriver {orbit=new Orbit {referenceBody=body,StartUT=100,EndUT=200}}};
            Check(TryCreateVesselSnapshot(vessel,slots,100,200,out _,out _),"interval within orbit patch");
            Check(!TryCreateVesselSnapshot(vessel,slots,99,200,out _,out _),"reject interval crossing patch start");
            Check(!TryCreateVesselSnapshot(vessel,slots,100,201,out _,out _),"reject interval crossing patch end");
            vessel.orbitDriver.orbit.referenceBody=new CelestialBody();
            Check(!TryCreateVesselSnapshot(vessel,slots,100,200,out _,out _),"reject changed SOI reference");
        }
        static void JobsAndBuffers()
        {
            const int bodyCount=3,vesselCount=3,sunCount=2,sampleCount=2;
            var times=Buffer<SubStepTimeNative>(4);
            times[0]=new SubStepTimeNative {UT=0,Duration=1}; times[1]=new SubStepTimeNative {UT=1,Duration=1};
            var bodies=Buffer<SubStepBodyNative>(5);
            bodies[0]=new SubStepBodyNative {ParentBody=-1,Radius=1,North=new double3(0,1,0),PrimeMeridian=new double3(1,0,0),East90=new double3(0,0,1)};
            bodies[1]=new SubStepBodyNative {ParentBody=-1,Radius=1,RootPosition=new double3(100,0,0)};
            bodies[2]=new SubStepBodyNative {ParentBody=-1,Radius=1,RootPosition=new double3(-100,0,0)};
            // These spare slots must not be read when a smaller batch reuses capacity.
            bodies[3]=new SubStepBodyNative {ParentBody=-1,Radius=10000,RootPosition=new double3(double.NaN)};
            bodies[4]=bodies[3];
            var positions=Buffer<double3>(12);
            for(int i=0;i<positions.Length;i++) positions[i]=new double3(double.NaN);
            var bodyJob=new SubStepBodyPositionJob {Times=times,Bodies=bodies,BodyCount=bodyCount,Positions=positions};
            for(int i=0;i<sampleCount;i++) bodyJob.Execute(i);
            ValidateBodyPositions(positions,sampleCount*bodyCount);
            Near(positions[3].x,0,0,"body stride uses active count");
            Check(double.IsNaN(positions[6].x),"body job leaves unused capacity untouched");
            var vessels=Buffer<SubStepVesselNative>(5);
            vessels[0]=new SubStepVesselNative {Trajectory=2,MainBody=0,Altitude=1};
            vessels[1]=new SubStepVesselNative {Trajectory=1,MainBody=0,Orbit=default}; // invalid vessel only
            vessels[2]=new SubStepVesselNative {Trajectory=2,MainBody=0,Altitude=1,Longitude=180};
            var geometry=Buffer<SubStepVesselGeometryNative>(12);
            var vesselJob=new SubStepVesselPositionJob {Times=times,Bodies=bodies,Vessels=vessels,BodyPositions=positions,BodyCount=bodyCount,VesselCount=vesselCount,Geometry=geometry};
            for(int i=0;i<sampleCount*vesselCount;i++) vesselJob.Execute(i);
            Near(geometry[3].Position.x,2,1e-12,"vessel stride uses active count");
            Near(geometry[5].Position.x,-2,1e-12,"third vessel second sample");
            var stars=Buffer<SubStepStarNative>(4);
            stars[0]=new SubStepStarNative {BodyIndex=1,FluxScale=9604};
            stars[1]=new SubStepStarNative {BodyIndex=2,FluxScale=10404};
            var suns=Buffer<SubStepSunNative>(24);
            for(int i=0;i<suns.Length;i++) suns[i]=new SubStepSunNative {RawFlux=double.NaN};
            var sunJob=new SubStepSolarVisibilityJob {Bodies=bodies,Vessels=vessels,BodyPositions=positions,VesselGeometry=geometry,Stars=stars,VesselCount=vesselCount,BodyCount=bodyCount,SunCount=sunCount,Results=suns};
            for(int i=0;i<sampleCount*vesselCount*sunCount;i++) sunJob.Execute(i);
            Near(suns[0].RawFlux,1,1e-12,"first star inverse square flux");
            Near(suns[1].RawFlux,1,1e-12,"second star inverse square flux");
            Check(suns[0].Visible==1 && suns[1].Visible==0,"opposite stars have independent eclipse visibility");
            Check(suns[4].Visible==0 && suns[5].Visible==1,"opposite side vessel sees other star");
            Check(suns[6].Visible==1 && suns[7].Visible==0,"solar stride uses active counts");
            Check(double.IsNaN(suns[12].RawFlux),"solar job leaves unused capacity untouched");
            Check(HasFiniteVesselResults(geometry,suns,0,vesselCount,sampleCount,sunCount),"first good vessel retained");
            Check(!HasFiniteVesselResults(geometry,suns,1,vesselCount,sampleCount,sunCount),"bad vessel rejected");
            Check(HasFiniteVesselResults(geometry,suns,2,vesselCount,sampleCount,sunCount),"last good vessel retained");
            NativeArray<double3> capacity=default;
            EnsureCapacity(ref capacity,8); capacity[0]=new double3(42);
            EnsureCapacity(ref capacity,2);
            Check(capacity.Length==8 && capacity[0].x==42,"smaller batch retains buffer");
            EnsureCapacity(ref capacity,9);
            Check(capacity.Length==16,"buffer grows geometrically");
            EnsureCapacity(ref capacity,8);
            Check(capacity.Length==16,"buffer does not shrink after growth");
        }
        static void Hierarchy()
        {
            var times=Buffer<SubStepTimeNative>(1);
            times[0]=new SubStepTimeNative {UT=Math.PI/2,Duration=1};
            var bodies=Buffer<SubStepBodyNative>(3);
            bodies[0]=new SubStepBodyNative {ParentBody=-1,RootPosition=new double3(1000,0,0)};
            bodies[1]=new SubStepBodyNative {ParentBody=0,Orbit=Orbit(0,10)};
            bodies[2]=new SubStepBodyNative {ParentBody=1,Orbit=Orbit(0,2)};
            var positions=Buffer<double3>(3);
            new SubStepBodyPositionJob {Times=times,Bodies=bodies,BodyCount=3,Positions=positions}.Execute(0);
            Near(positions[2].x,1000,1e-10,"moon inherits root translation");
            Near(positions[2].y,12,1e-10,"moon and parent evaluated at same UT");
        }
        public static string Run()
        {
            Conics(); RotationAndPatches(); JobsAndBuffers(); Hierarchy();
            return "PASS: "+assertions+" orbit, rotation, patch, multi-star and buffer checks (managed Jobs harness; not Burst execution)";
        }
    }
}
