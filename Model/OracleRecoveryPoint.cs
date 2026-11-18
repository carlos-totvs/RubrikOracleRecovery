using System;

namespace RubrikOracleRecovery.Model
{
    public class OracleRecoveryPoint
    {
        public string HostOrRacName { get; set; }
        public DateTime LatestEndTime { get; set; }
    }
}