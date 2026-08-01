# Feel Transform Feedbacks

## Available Feedbacks
- **Position**: lets you tweak the position of a transform over time with different modes : A to B will move the object from an initial to a destination position, at the specified speed, duration and acceleration. Along Curve will move the object along the defined curve, with remapped values, on any or all of the 3 axes. To Destination will move the object to the specified destination.
- **Rotation**: lets you play with the rotation of a transform over time, packed with options. Similar to the position feedback, you can rotate an object in absolute mode, additive (adding to its current rotation at the start of the play), or to a defined destination.
- **Position Spring**: move or bump a target’s position using a spring.
- **Rotation Spring**: move or bump a target’s rotation using a spring.
- **Scale Spring**: move or bump a target’s scale using a spring.
- **Wiggle**: lets you play with rotation, scale and position over time. You’ll need an MMWiggle component on your target object for this to work.
- **Rotate Position Around**: lets you rotate a target object around another center object, with full axis control.
- **DestinationTransform**: lets you animate all properties (position, rotation, scale) of a transform to match a destination transform’s properties
- **SquashAndStretch**: modify the scale of an object on an axis while the other two axis (or only one) get automatically modified to conserve mass. This requires a normalized scale (see note below).
- **Position Shake**: lets you activate a target position shaker. The shaker will move its target object’s position for the specified duration, within a certain range and along a certain direction. You can control the randomness of that shake, as well as its attenuation over time. This requires one or more MMPositionShaker(s).
- **Rotation Shake**: lets you activate a target rotation shaker. The shaker will move its target object’s position for the specified duration, within a certain range and along a certain direction. You can control the randomness of that shake, as well as its attenuation over time. This requires one or more MMRotationShaker(s).
- **Scale Shake**: lets you activate a target scale shaker. The shaker will move its target object’s position for the specified duration, within a certain range and along a certain direction. You can control the randomness of that shake, as well as its attenuation over time. This requires one or more MMScaleShaker(s).
- **Look At**: lets you rotate a Transform to have it face another target transform (or a direction, or specific world coordinates), complete with optional axis locks and event/shaker support, in which case you’ll need a MMLookAtShaker on your target(s).
- **Set Parent**: lets you change the parent Transform of a target Transform

