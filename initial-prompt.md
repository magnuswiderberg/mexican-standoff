# Mexican standoff
Lets create the party game "Mexican Standoff - A Quick Mind Game".

First we plan, find the tech stack and find features and document them.

Please ask me questions so that we can make the game experience as good as possible for the players. Also read the Ideas section, and see if something here rhymes and should be part of MVP.

# Overall
Around 2-8 players can join the game and they each get an unloaded gun. The goal of the game is to either eliminate all opponents or be the first to find 3 gold bars.

The game is played in rounds where each player selects an action and then all players reveal their action at the same time. The game resolves the action in a specific order and determines what happens.

If a player is out of HP (Health Points), they are eliminated from the game. The games are supposed to be quick (max 10 min), so if the party decides to play again, eliminated players should be offered to join from the page they're on.

# Game mechanics

## Playing
1. Each player selects an action card to play
2. When all have selected, cards are revealed all at once
3. Action events are carried out in order

## Health Points
Each player starts out with 2 HP counters. If hit by an opponent's gun shot, they lose one counter per shot. If they reach zero (or less) they get wounded and are elmininated.

## Treasue chests
Depending on the number of players left in the game, there are one or two treasure chests available to the players. If a player is alone on a chest (and not shot at), they get a gold bar from it.

- 2-4 players: 1 chest
- 5-8 players: 2 chests

## Gun
Each player has a gun, which is unloaded at first. They can use an action to load it and it can hold 2 bullets. While loaded, it can be used to shoot other players.

## Actions
Listed below are the available actions players have each round. It also specifies in which order the action events are resolved.

1. Dodge: When dodging, a player can't be hit by gun shots.
2. Attack: Requires a loaded gun. Target player may not carry out their action. Two players can shoot each other and the shots hit at the same time. A player dies if out of HP counters. When attacking, one bullet is used up from the gun.
3. Load: Loads the gun with a bullet. A player may have up to 2 bullets in the gun.
4. Chest: A player tries to get a coin from a chest (if more than one chest, must specify which). If alone on the chest, the player gets a coin. The maximum number of players on the chest to get a coin varies with the number of players currently in the game.

## Elimination
A player that looses all their HP is considered wounded, and is out of the game. The players that shot the eliminated player get your coins, split among them rounded down.

## Winner
- The player first to reach 3 gold bar wins.
- A single player left wins.
- If several players get 3 gold bars, the one with the most bars wins.
- If they have the same amount of bars, the player with the most HP left wins.
- If they have the same amount of HP, the player with the most bullets in the gun wins.
- If they have the same amount of bullets in the gun, they all win.

## Special mode with 2 players
The game mode shifts when there are only 2 players left (or maybe the game started with 2 players, if we allow that). Maybe this requires a different way of selecting the actions; otherwise there could be loops where both players are out of bullets and both always go for the chest.

Maybe they each choose 3 actions in a sequence and then the game resolves them one by one?

# Game devices
There a two scenarios:
1. The party has access to a monitor, where we can show the game state, reveal the reuslts of each round and crown the winner.
2. There are only players with mobile phones.

With a monitor device available, it's straight forward to build a Monitor page where at first a QR code is shown for the mobile devices to scan. Then start the game with button click, and show players with their stats, what state the game is in, reveal results of a round in a dramatic way, and at the end crown the winner.

Lets discuss if there is a way to play without a monitor also, that can be equally engaging and fun.

# Tech stack options
I have access to Azure and an existing app service plan and existing Cosmos DB, so that's a preference. Since this is hobby project, we need to minimize the costs.

Frontend options I am comfortable with: React and Blazor.
Backend options: .NET, maybe Node

We should be using bicep for infra-as-code.

We should use SignalR for a snappy game experience, both for the device pages and the Monitor page.

If we _really_ need an SQL database, we could spin up a Supabase project, but I think I would rather like to use Cosmos. Or maybe we don't need a database at all in MVP?

It's important to have a good DEV experience, where I can use Docker containers locally for Cosmos (or Supabase/PostgrSQL), etc. We will also use TestContainers in our integration test project.

We shall also create a unit test project where we test the game engine properly.

# Finding game parameters
We should run game simulations to find out what parameters would fit best for a fairly quick game. We could create bots with different strategies that play thousands of games with different parameters and compare the results.

- Number of chests for number of players (maybe use 3 chests at some threshold?)
- Number of gold bars to win
- Maximum number of bullets in the gun
- Number of initial HP counters

# Bring your bot
Not part of MVP, but maybe later, we could offer players to bring their own bot to play. That would be a URL that fulfills a certain contract that the game engine calls throughout the game.

# Ideas
- Healing? Should be expensive; maybe a 2 gold bars
- Asymmetric setup: some a gold bars, some a loaded gun, extra HP, etc.
- Only ever _one_ chest: makes the initial part of the game more focused on loading/attacking
- Game accounts for easy access and saved stats
- Admin panel to see stats
