using System.Collections.Generic;

namespace Core.Block
{
    public class BlockStateContainer
    {
        private Dictionary<string, BlockState> states = new();

        public void SetState(string name, string value)
        {
            states[name] = new BlockState(name, value);
        }

        public string GetState(string name)
        {
            return states.TryGetValue(name, out var state)
                ? state.value
                : null;
        }

        public bool HasState(string name) => states.ContainsKey(name);

        public bool IsStateless()
        {
            return states.Count == 0;
        }

        public Dictionary<string, BlockState> GetAllStates => states;
    }
}