namespace Mis6
{
    internal class CpuFunctionDiscard : CpuFunction
    {
        public override byte Get()
        {
            return 0;
        }

        public override void Set(byte value)
        {
            // nothing
        }
    }
}