namespace Core.Block
{
    public class BlockState
    {
        public string stateName; // "facing", "growth", etc.
        public string value;     // "north", "1", "true", etc.

        public BlockState(string name, string value)
        {
            this.stateName = name;
            this.value = value;
        }
    }
}
