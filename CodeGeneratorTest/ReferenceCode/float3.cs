namespace Unity.Mathematics {
    public record float3(float x, float y, float z) {
        public static float3 operator+(float3 a, float3 b) => new float3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static float3 operator-(float3 a, float3 b) => new float3(a.x - b.x, a.y - b.y, a.z - b.z);
    }
    
    public record float2(float x, float y) {
        public static float2 operator+(float2 a, float2 b) => new float2(a.x + b.x, a.y + b.y);
        public static float2 operator-(float2 a, float2 b) => new float2(a.x - b.x, a.y - b.y);
    }
}