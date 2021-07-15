# EntityNetworkingSystems
 A networking framework for Unity.


I have tried many different networking systems for Unity. None have come to my liking so I will be coming together, to make one that seems to work.

I am mainly using this for my project at https://epocria.net

[Documentation](https://github.com/AncientEntity/EntityNetworkingSystems/wiki)

## Important Features
- Packet Sending Backbone (Complete)
- Instantiating/Destroying GameObjects over the network (Complete)
- Player authority over an object. (Complete)
- Network Fields, automatically get synced. (Complete)
- RPC with Arguments (Complete)
- Steamworks Integration (Sort Of/In Progress)
- Higher Level Methods. Kicking/Banning/Etc. (Soon)
- EntityNetworkingSystems Namespace (Complete)
- Documentation (Always being worked on)
- [More Here](https://github.com/AncientEntity/EntityNetworkingSystems/wiki/Full-Todo-List)
- InputWorker, manages input over the network (Complete)

## Extras
- Buffered Packet Culling (Complete)
- NetworkField's will automatically convert Vectors/Quaternions to Serializable Variants. (In Progress oh yeah!
- Prefab Domain Templates (Complete)
- Generic Type Parameters for NetworkFields (Complete)
- On Unity Asset Store (Last)

## Contributing & Discord
Contributing in anyway possible means a lot. If you see something you can improve, something you want added, or just want to do some documentation make a branch then let me know through GitHub when it is ready! As well as if you want to communicate with me, I am using this for my game "Epocria" which has a Discord server I can be contacted through: https://discord.gg/vkxmjrx

## Requirements & License
- Unity 2019 or above. (It hasn't been tested on Unity 2018, but I assume it should be fine)
- ENS uses Facepunch.Steamworks (https://github.com/Facepunch/Facepunch.Steamworks) which is under their own MIT license as well. Please check their LICENSE.MD for additional information, or our LICENSE.MD for additional information. It comes packaged within Entity Networking Systems, so no separate installation needed.
- ENS uses Open.NAT for automatic port forwarding (UPnP) and is also under their own MIT License, check there LICENSE.MD for additional information: https://github.com/lontivero/Open.NAT
