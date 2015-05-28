
namespace CSL_Traffic
{
    class NetInfoLane : NetInfo.Lane
    {
        public enum SpecialLaneType
        {
            None,
            BusLane,
            PedestrianLane
        }

        public RoadManager.VehicleType m_allowedVehicleTypes;
        public SpecialLaneType m_specialLaneType;


        public NetInfoLane(RoadManager.VehicleType vehicleTypes, SpecialLaneType specialLaneType = SpecialLaneType.None)
        {
            this.m_allowedVehicleTypes = vehicleTypes;
            this.m_specialLaneType = specialLaneType;
        }

        public NetInfoLane(NetInfo.Lane lane, SpecialLaneType specialLaneType = SpecialLaneType.None) : this(lane, RoadManager.VehicleType.All, specialLaneType) { }

        public NetInfoLane(NetInfo.Lane lane, RoadManager.VehicleType vehicleTypes, SpecialLaneType specialLaneType = SpecialLaneType.None) : this(vehicleTypes, specialLaneType)
        {
            CopyAttributes(lane);
        }

        void CopyAttributes(NetInfo.Lane lane)
        {
            this.m_position = lane.m_position;
            this.m_width = lane.m_width;
            this.m_verticalOffset = lane.m_verticalOffset;
            this.m_stopOffset = lane.m_stopOffset;
            this.m_speedLimit = lane.m_speedLimit;
            this.m_direction = lane.m_direction;
            this.m_laneType = lane.m_laneType;
            this.m_vehicleType = lane.m_vehicleType;
            this.m_laneProps = lane.m_laneProps;
            this.m_allowStop = lane.m_allowStop;
            this.m_useTerrainHeight = lane.m_useTerrainHeight;
            this.m_finalDirection = lane.m_finalDirection;
            this.m_similarLaneIndex = lane.m_similarLaneIndex;
            this.m_similarLaneCount = lane.m_similarLaneCount;
        }

    }
}
