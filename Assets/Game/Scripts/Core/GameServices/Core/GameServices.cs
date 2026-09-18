using SpaceGame.Gameplay;

namespace SpaceGame.Core
{
    public static class GameServices
    {
    
        public static void Initialize()
        {
            LoadServices(Game.Mode);

            Game.OnGameModeChanged += LoadServices;
        }
    
        static void LoadServices(GameMode mode)
        {
            ItemDropService = new PlayerDropService();
            World = new WorldService();
        }

        public static IItemDropService ItemDropService { get; set; }
    
        public static IWorldService World { get; set; }
    }
}
