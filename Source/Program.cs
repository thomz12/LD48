using JuiceboxEngine;

namespace LD48
{
    class Program
    {
        static void Main(string[] args)
        {
            JuiceboxGame game = new JuiceboxGame();

            game.AudioManager.SetVolume(0.75f);

            game.Run(new MainMenu(game.ResourceManager));
        }
    }
}
