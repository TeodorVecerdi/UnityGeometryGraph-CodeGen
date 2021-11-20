namespace Unity.Mathematics {
    public static class math {
        public static float distancesq(float3 lhs, float3 rhs) {
            float3 diff = lhs - rhs;
            return dot(diff, diff);
        }
        
        public static float distancesq(float2 lhs, float2 rhs) {
            float2 diff = lhs - rhs;
            return dot(diff, diff);
        }

        
        public static float dot(float3 lhs, float3 rhs) {
            return lhs.x * rhs.x + lhs.y * rhs.y + lhs.z * rhs.z;
        }
        
        public static float dot(float2 lhs, float2 rhs) {
            return lhs.x * rhs.x + lhs.y * rhs.y;
        }
    }
}