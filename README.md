# HackMatcher
An AI player for the EXAPUNKS minigame "HACK*MATCH"

<div>
  <a href="https://www.youtube.com/watch?v=gfVJmm2CHus">
    <img src ="https://img.youtube.com/vi/gfVJmm2CHus/maxresdefault.jpg" />
    <p align="center">watch it play</p>
  </a>
</div>

## How to use
In the EXAPUNKS options, make it a 1366x768 window and disable the CRT effect in HACK*MATCH. Navigate to the HACK*MATCH title screen and run.

## What's broken?
More like "what's *not* broken?" The agent definitely won't get 100,000+ points every time; you may need to leave it going for an hour or so.
* Search is only fast enough to handle ~25K game states on my 6th-gen i7. You may need to lower this on slower processors to get the agent playing at a reasonable speed. The low node count means that it will often miss good, complex moves.
* Piece recognition isn't 100%. It sometimes mistakes bombs for each other, and red/pink/purple pieces very rarely.
* Inputs occasionally get dropped, which is why I have it reset to the first column after every move. When grab/drops are missed, you'll see the agent add pieces to an already tall column when it thinks it's removing them. If input was more consistent, the agent could be sped up and the occasional stupid game over could be prevented.

## Standard Disclaimer
The code is crappy. Sorry! Do whatever you want with it, with the sole exception of judging me by its quality.
