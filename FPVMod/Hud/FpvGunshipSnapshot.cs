using FPVMod.Drone;
using FPVMod.Link;
using FPVMod.Session;
using UnityEngine;

namespace FPVMod.Hud
{
    /// <summary>Minimal snapshot for owned Gunship FS (MC HudSnapshot subset).</summary>
    internal readonly struct FpvGunshipSnapshot
    {
        private static float _nextLrfUnscaled;
        private static float _cachedLrf;

        internal bool HasFeed { get; }
        internal string Callsign { get; }
        internal float HeadingDeg { get; }
        internal float SpeedKmh { get; }
        internal float AltM { get; }
        internal float PosX { get; }
        internal float PosZ { get; }
        internal float Batt01 { get; }
        internal float Col01 { get; }
        internal string Fuse { get; }
        internal string Link { get; }
        internal float RangeM { get; }
        internal float FeedFovDeg { get; }
        internal string GridText { get; }

        private FpvGunshipSnapshot(
            bool hasFeed,
            string callsign,
            float headingDeg,
            float speedKmh,
            float altM,
            float posX,
            float posZ,
            float batt01,
            float col01,
            string fuse,
            string link,
            float rangeM,
            float feedFovDeg,
            string gridText)
        {
            HasFeed = hasFeed;
            Callsign = callsign;
            HeadingDeg = headingDeg;
            SpeedKmh = speedKmh;
            AltM = altM;
            PosX = posX;
            PosZ = posZ;
            Batt01 = batt01;
            Col01 = col01;
            Fuse = fuse;
            Link = link;
            RangeM = rangeM;
            FeedFovDeg = feedFovDeg;
            GridText = gridText;
        }

        internal static FpvGunshipSnapshot Empty => new FpvGunshipSnapshot(
            false, "FPV-1", 0f, 0f, 0f, 0f, 0f, 0f, 0f, "SAFE", "LOST", 0f, FpvConstants.CameraFov, "---");

        internal static FpvGunshipSnapshot Build(Missile? drone, float feedFov)
        {
            if (drone == null || drone.disabled)
                return Empty;

            FpvAcroController? ac = drone.GetComponent<FpvAcroController>();
            FpvWarhead? wh = drone.GetComponent<FpvWarhead>();
            FpvLinkLevel link = FpvLinkQuality.Evaluate(drone, FpvControlSession.Launcher);

            float alt;
            try { alt = drone.radarAlt; }
            catch { alt = drone.transform.position.y; }

            GlobalPosition gp = default;
            try { gp = drone.transform.GlobalPosition(); }
            catch
            {
                Vector3 p = drone.transform.position;
                gp = new GlobalPosition(p.x, p.y, p.z);
            }

            float rng = 0f;
            // LRF is visual aid — don't SphereCast every HUD frame (free-look FPS).
            if (Time.unscaledTime >= _nextLrfUnscaled)
            {
                _nextLrfUnscaled = Time.unscaledTime + 0.1f;
                Vector3 o = drone.transform.position;
                Vector3 d = drone.transform.forward;
                if (Physics.Raycast(o, d, out RaycastHit hit, 5000f, PhysicsLayers.StaticsMask | PhysicsLayers.ShipsMask))
                    _cachedLrf = hit.distance;
                else
                    _cachedLrf = 0f;
            }
            rng = _cachedLrf;

            string fuse = wh != null && wh.IsSafe ? "SAFE" : "ARMED";
            string linkTxt = link switch
            {
                FpvLinkLevel.Full => "GOOD",
                FpvLinkLevel.Degraded => "WEAK",
                _ => "LOST"
            };

            string name = "FPV DRONE";
            try
            {
                if (drone.definition != null && !string.IsNullOrEmpty(drone.definition.unitName))
                    name = drone.definition.unitName;
            }
            catch { /* ignore */ }

            return new FpvGunshipSnapshot(
                hasFeed: true,
                callsign: name,
                headingDeg: Mathf.Repeat(drone.transform.eulerAngles.y, 360f),
                speedKmh: ac?.SpeedKmh ?? 0f,
                altM: Mathf.Max(0f, alt),
                posX: gp.x,
                posZ: gp.z,
                batt01: ac?.Battery01 ?? 0f,
                col01: ac?.Collective01 ?? 0f,
                fuse: fuse,
                link: linkTxt,
                rangeM: rng,
                feedFovDeg: feedFov > 1f ? feedFov : FpvConstants.CameraFov,
                gridText: "---");
        }
    }
}
