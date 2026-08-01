# Feel GameObject Feedbacks

## Available Feedbacks
- **Destroy**: lets you destroy, destroy immediate or disable a specific game object
- **Enable Behaviour**: enables or disables a monobehaviour when the feedback plays, inits, stops or resets.
- **Float Controller**: possibly the most powerful of all the feedbacks, this one lets you control a float value on any monobehaviour. You’ll need a FloatController component on your target mono.
- **Instantiate Object**: spawns objects when the feedback plays, at the specified position.
- **Rigidbody**: adds force or torque to a Rigidbody
- **Rigidbody2D**: adds force or torque to a Rigidbody2D
- **Collider**: enable/disable/toggle a target collider, or change its trigger status
- **Collider2D**: enable/disable/toggle a target collider 2D, or change its trigger status
- **Layer**: change the layer of a target game object on play
- **Property**: lets you target and control any property or field (floats, vectors, ints, strings, colors, etc), on any object (including ScriptableObjects), and control it over time. Drag a game object or scriptable object into its TargetObject slot, then select a component, and finally a property you want to affect. From there you can define remap options, and tweak your curve.
- **Set Active**: sets an object active or inactive
- **MMRadioSignal**: this feedback lets you control a MMRadioSignal, that can then be broadcasted to control receivers, to pilot any value you want on any component, on any object. Don’t hesitate to read more about the MMRadio system, it can prove quite useful!
- **MMRadio Broadcast**: similar to the MMRadioSignal one, but directly broadcasts the signal to any receivers, instead of going through an emitter

