
namespace CSL_Traffic
{
    sealed class NetInfoLane : NetInfo.Lane
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
            m_allowedVehicleTypes = vehicleTypes;
            m_specialLaneType = specialLaneType;
        }

        public NetInfoLane(NetInfo.Lane lane, SpecialLaneType specialLaneType = SpecialLaneType.None) : this(lane, RoadManager.VehicleType.All, specialLaneType) { }

        public NetInfoLane(NetInfo.Lane lane, RoadManager.VehicleType vehicleTypes, SpecialLaneType specialLaneType = SpecialLaneType.None) : this(vehicleTypes, specialLaneType)
        {
            CopyAttributes(lane);
        }

        void CopyAttributes(NetInfo.Lane lane)
        {
            m_position = lane.m_position;
            m_width = lane.m_width;
            m_verticalOffset = lane.m_verticalOffset;
            m_stopOffset = lane.m_stopOffset;
            m_speedLimit = lane.m_speedLimit;
            m_direction = lane.m_direction;
            m_laneType = lane.m_laneType;
            m_vehicleType = lane.m_vehicleType;
            m_laneProps = lane.m_laneProps;
            m_allowStop = lane.m_allowStop;
            m_useTerrainHeight = lane.m_useTerrainHeight;
            m_finalDirection = lane.m_finalDirection;
            m_similarLaneIndex = lane.m_similarLaneIndex;
            m_similarLaneCount = lane.m_similarLaneCount;
        }

    }
}
