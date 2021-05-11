# LD48
Our Ludum Dare 48 game: Debt Collector 💰 Play here! https://thomz12.github.io/DebtCollector/

## Info
Made using my Juicebox game engine. The Ludum Dare page can be found here: https://ldjam.com/events/ludum-dare/48/debt-collector

> In DEBT COLLECTOR 💰 the goal is to collect as much debt as possible before running out of time. Spend money on random items and add a dollar to your debt by pressing the SHOP-button over and over again. Or, to increase your debt even quicker, take out some subscriptions and loans that will automatically increase your debt every second 📈! Will you be the best debt collector and up up with the deepest/highest debt before your credit card gets blocked? Try it out and see where you end up on our leaderboard! Will you take a top spot? 👀

## Building
Building should be very easy. In the `Source/` folder, run the following command:
```
dotnet build
```
This should install the Nuget packages, compiler and kick of a build. Building can also be done from Visual Studio 2019. In the output directory there should be a `h5` folder containing all required resources to play the game. You can host a local webserver from this folder to test.
