# Feel Renderer Feedbacks

## Available Feedbacks
- **Flicker**: lets you rapidly change the color of a material. By default this will control the target renderer’s shader’s Color value but the feedback also lets you specify your own if you want. Comes with flicker duration, octave and color control. You can also provide a list of extra target renderers, in which case all will flicker at once.
- **Fog**: lets you animate the density, color, end and start distance of your scene’s fog
- **Material**: changes the material of the target renderer everytime it’s played, out of an array of materials. You can swap them sequentially or randomly.
- **Line Renderer**: lets you update a line renderer’s width and color over time.
- **Material Set Property**: set the value of the property of your choice on the target renderer’s material.
- **MMBlink**: controls an MMBlink, letting you do advanced blinking behaviours, either by enabling/disabling a gameobject, changing its alpha, emission intensity, or a value of your choice on a shader), with or without interpolation, and will let you define repeat patterns and phases.
- **Shader Controller**: similar to the Float Controller, lets you control most settings of any shader. Will require a ShaderController component on your target (or targets, it lets you control more than one at once)
- **Shader Global**: lets you control global shader properties at runtime.
- **Sprite**: change the sprite on a target sprite renderer
- **SpriteRenderer**: take control of a SpriteRenderer’s color and X or Y flip
- **SpriteRenderer Alpha**: lets you animate the alpha of a target sprite renderer over time, regardless of its color
- **Skybox**: lets you assign a new material (at random or not) to change the scene’s skybox to a new one
- **TextureOffset**: lets you control a target renderer’s material’s texture offset over time
- **TextureScale**: lets you control a target renderer’s material’s texture scale over time
- **Trail Renderer**: lets you update a trail renderer’s length, width and color over time

