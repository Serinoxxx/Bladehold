# Feel Audio Feedbacks

## Available Feedbacks
- **AudioSource**: lets you play, pause, unpause or stop a preexisting audiousource on demand. You’ll also be able to play/pause/stop/resume it at a random pitch and volume, and through an optional audio mixer group.
- **Sound**: another way to trigger a sound. You specify an audio clip, and then you can decide to have it instantiated on demand, cached, have it create an object pool of ready to use sounds, or even trigger a sound event. This feedback also lets you preview your sound from the editor. Note that by default it’s set to Event mode, which requires a MMSoundManager be present in the scene to catch it (or any other class of yours you’d like to catch such events). If you don’t plan on using a sound manager, Cached mode is probably what you’ll want to go with.
- **AudioSource Pitch**: tweak an AudioSource’s pitch over time. You will need a MMAudioSourcePitchShaker on your target audiosource(s).
- **AudioSource Stereo Pan**: alter the stereo pan value of an AudioSource over time. You will need a MMAudioSourceStereoPanShaker on your target audiosource(s).
- **AudioSource Volume**: lets you tween the volume of an audio source over time. You will need a MMAudioSourceVolumeShaker on your target audiosource(s).
- **Distortion Filter**: tween the distortion level of a distortion filter over time. You will need a MMAudioSourceDistortionShaker on your target audiosource(s), as well as a distortion filter.
- **Echo Filter**: tween echo over time. You will need a MMAudioSourceEchoShaker on your target audiosource(s), as well as a echo filter.
- **High Pass Filter**: tween the cutoff of a high pass over time. You will need a MMAudioSourceHighPassShaker on your target audiosource(s), as well as a high pass filter.
- **Low Pass Filter**: tween the cutoff of a low pass over time. You will need a MMAudioSourceLowPassShaker on your target audiosource(s), as well as a low pass filter.
- **Reverb Filter**: tween reverb levels over time. You will need a MMAudioSourceReverbShaker on your target audiosource(s), as well as a Reverb filter.
- **AudioMixer Snapshot Transition**: lets you transition to a target snapshot over a specified duration
- **MMPlaylist**: lets you remote control (play/pause/stop/previous/next/etc) a MMPlaylist from a feedback.
- **MMSoundManager All Sounds Control**: control all sounds playing on a MMSoundManager
- **MMSoundManager Save and Load**: save and load MMSoundManager settings (track volume, etc)
- **MMSoundManager Sound**: lets you play a sound on the MMSoundManager
- **MMSoundManager Sound Control**: lets you play/pause/resume/setVolume/more on a sound playing on the MMSoundManager
- **MMSoundManager Sound Fade**: lets you fade sounds in/out on the MMSoundManager
- **MMSoundManager Track Control**: lets you control entire tracks (music, UI, sfx, master) on a MMSoundManager
- **MMSoundManager Track Fade**: lets you fade the tracks of the MMSoundManager

