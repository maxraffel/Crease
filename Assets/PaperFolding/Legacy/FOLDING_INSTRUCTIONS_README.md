# Folding Instructions System

A comprehensive system for creating, editing, and playing back sequences of paper folding operations with camera movements.

## Overview

The Folding Instructions system allows you to:
- Create reusable folding sequences as ScriptableObjects
- Define complex fold operations with boolean tag expressions
- Add camera movements between folds
- Validate tag references and expressions
- Play back sequences at runtime with animation

## Components

### 1. FoldingInstructions (ScriptableObject)

**Location**: Create via `Assets > Create > Paper Folding > Folding Instructions`

Stores a complete sequence of folding steps and camera movements.

**Properties**:
- `Sequence Name`: Display name for the instruction set
- `Description`: What this sequence creates
- `Steps`: List of fold and camera move steps
- `Auto Play`: Whether to start automatically
- `Loop`: Whether to repeat when finished

### 2. FoldStep Classes

#### FoldStepData
Represents a single fold operation:
- `Handle UV`: Position on the paper edge (0-1 coordinates)
- `Tag Name`: Tag to apply to affected vertices
- `Tag Expression`: Boolean filter for which vertices to fold
- `Fold Angle`: Degrees to fold (-180 to 180)
- `Duration`: Animation time (0 = instant)

#### CameraMoveStep
Represents a camera movement:
- `Rotation`: Target Euler angles
- `Distance`: Distance from paper
- `Duration`: Movement time in seconds
- `Ease Curve`: Animation curve for smooth motion

### 3. FoldingInstructionsPlayer (Component)

Runtime player that executes instruction sequences.

**Setup**:
1. Add to GameObject with PaperMesh
2. Assign FoldingInstructions asset
3. Assign references (PaperMesh, FoldController, Camera)
4. Optional: Set up camera pivot point

**Controls** (via Inspector in Play Mode):
- Play: Start from beginning
- Pause: Pause playback
- Stop: Stop and reset to step 0
- Reset: Return paper and camera to initial state
- Prev/Next: Manual step navigation

## Creating a Folding Sequence

### Step 1: Create the Asset
1. Right-click in Project window
2. `Create > Paper Folding > Folding Instructions`
3. Name your sequence (e.g., "Paper Crane Instructions")

### Step 2: Add Fold Steps

Click "➕ Add Fold Step" to create a new fold:

1. **Handle UV**: Set the position where the fold originates
   - (0.5, 0) = middle of bottom edge
   - (1.0, 0.5) = middle of right edge
   - Use "Snap to Edge" button to auto-snap

2. **Tag Name**: Name for this fold layer
   - Example: `fold_1`, `wing_left`, `tip`
   - This tag will be applied to all moved vertices

3. **Tag Expression**: Filter which vertices can fold
   - Leave empty to fold all vertices
   - Use boolean logic with previously created tags
   
   **Examples**:
   ```
   fold_1                    → Only fold vertices tagged with fold_1
   fold_1 AND fold_2         → Fold vertices with BOTH tags
   fold_1 OR fold_2          → Fold vertices with EITHER tag
   NOT fold_1                → Fold all EXCEPT fold_1 vertices
   (fold_1 OR fold_2) AND NOT fold_3  → Complex expression
   ```

4. **Fold Angle**: Rotation in degrees
   - 180° = fold completely over
   - 90° = perpendicular fold
   - -90° = fold in opposite direction

5. **Duration**: Animation time
   - 0 = instant
   - 1.0 = fold over 1 second

### Step 3: Add Camera Moves

Click "➕ Add Camera Move" to add camera animation:

1. **Rotation**: Target camera angle
   - Presets available: Front, Top, Side, Iso

2. **Distance**: How far from the paper

3. **Duration**: Movement time

4. **Ease Curve**: Animation curve for smooth motion

### Step 4: Organize Steps

- Click step headers to expand/collapse details
- Use ▲▼ buttons to reorder
- Use ✕ to delete
- Select steps to edit in detail

## Tag System

### Tag Creation
- Each fold step creates a new tag
- Tags are applied to vertices that move during the fold
- Tags accumulate (vertices can have multiple tags)

### Tag Expressions
The expression evaluator supports:
- **AND**: Both conditions must be true
- **OR**: Either condition can be true
- **NOT**: Invert condition
- **( )**: Group conditions

### Tag Validation
The editor automatically:
- ✅ Validates expression syntax
- ⚠️ Warns about undefined tag references
- 📊 Shows which tags are available at each step
- 🔍 Displays tag timeline and usage

### Available Tags Display
When editing a fold step, the editor shows:
- Tags created by previous steps (available for use)
- Quick-add buttons for common tags
- Real-time expression validation

## Editor Features

### Toolbar
- **Show Validation**: Display all expression errors and warnings
- **Show Tag Analysis**: View tag creation timeline and references
- **Clear All**: Remove all steps (with confirmation)

### Step Indicators
- 📄 = Fold step
- 🎥 = Camera move step
- Colors:
  - Blue = Fold step
  - Orange = Camera step
  - Green = Selected
  - Yellow = Warning (undefined tags)
  - Red = Error (invalid expression)

### Validation Panel
Shows:
- Expression syntax errors
- Undefined tag references
- Links to problematic steps

### Tag Analysis Panel
Shows:
- All tags created by sequence
- All tags referenced in expressions
- Unreferenced tags
- Tag creation timeline

## Runtime Playback

### Automatic Playback
```csharp
public FoldingInstructions instructions;
public FoldingInstructionsPlayer player;

void Start()
{
    player.Instructions = instructions;
    player.Play();
}
```

### Manual Control
```csharp
// Play/Pause/Stop
player.Play();
player.Pause();
player.Resume();
player.Stop();

// Step through
player.ExecuteStep(0);  // Execute first step
player.CurrentStepIndex = 5;
player.ExecuteStep(player.CurrentStepIndex);

// Reset
player.Reset();  // Clear all folds and reset camera

// Check status
if (player.IsPlaying)
{
    Debug.Log($"Currently on step {player.CurrentStepIndex}");
}
```

### Animation
- Folds with `duration > 0` animate smoothly
- Camera moves use customizable ease curves
- Steps execute sequentially with small delays

## Examples

### Simple Valley Fold
```
Step 0: Fold
  Handle UV: (0.5, 0)
  Tag: valley_fold
  Expression: (empty - affects all)
  Angle: 180°
```

### Origami Crane Wing (Simplified)
```
Step 0: Fold diagonal
  Handle UV: (0, 0) to (1, 1)
  Tag: diagonal_1
  Angle: 180°

Step 1: Fold wing
  Handle UV: (0.25, 0.5)
  Tag: wing_left
  Expression: diagonal_1
  Angle: 90°

Step 2: Fold wing
  Handle UV: (0.75, 0.5)
  Tag: wing_right
  Expression: diagonal_1 AND NOT wing_left
  Angle: 90°
```

### With Camera Movement
```
Step 0: Camera - Top View
  Rotation: (90, 0, 0)
  Distance: 10
  Duration: 1.5s

Step 1: Fold
  [fold configuration]

Step 2: Camera - Rotate Around
  Rotation: (30, 45, 0)
  Distance: 8
  Duration: 2s
```

## Tips & Best Practices

1. **Tag Naming**: Use descriptive, hierarchical names
   - ✅ `base_fold`, `wing_left_tip`, `corner_tl`
   - ❌ `f1`, `x`, `temp`

2. **Tag Expressions**: Keep them simple when possible
   - Complex expressions are harder to debug
   - Use parentheses for clarity

3. **Step Organization**:
   - Add camera moves before complex folds
   - Group related folds together
   - Use validation panel frequently

4. **Testing**:
   - Test each step individually using "Execute" button
   - Use validation warnings as guides
   - Preview in editor before runtime

5. **Handle Positions**:
   - Always snap handles to edges
   - Handles should be on paper boundary
   - Use symmetric positions for symmetric folds

## Troubleshooting

### "Undefined tags" Warning
- A step references a tag not yet created
- Check tag timeline in Tag Analysis panel
- Reorder steps or fix expression

### "Expression error"
- Invalid boolean syntax
- Common issues:
  - Missing parentheses
  - Typo in tag name
  - Using && or || instead of AND/OR

### Fold Not Working
- Check tag expression matches expected vertices
- Verify handle is on edge (use Snap to Edge)
- Ensure fold angle is non-zero

### Camera Not Moving
- Verify Camera reference is assigned in Player
- Check duration > 0
- Ensure in Play Mode for runtime execution

## Advanced Usage

### Custom Fold Logic
Extend `FoldStepData` to add custom fold types:
```csharp
[System.Serializable]
public class CustomFoldStep : FoldStepData
{
    public MyCustomData customData;
    // Add custom execution logic
}
```

### Expression Extensions
Add new operators to `BooleanExpressionEvaluator`:
```csharp
// Add XOR support
if (expr.Contains("XOR")) { ... }
```

### Dynamic Sequences
Generate instructions at runtime:
```csharp
var instructions = ScriptableObject.CreateInstance<FoldingInstructions>();
instructions.AddFoldStep(new FoldStepData { ... });
player.Instructions = instructions;
player.Play();
```

## File Structure

```
Assets/Scripts/PaperFolding/
├── FoldStep.cs                    // Step class definitions
├── FoldingInstructions.cs         // Main ScriptableObject
├── FoldingInstructionsPlayer.cs   // Runtime player
├── BooleanExpressionEvaluator.cs  // Tag expression parser
└── Editor/
    ├── FoldingInstructionsEditor.cs       // Custom inspector
    └── FoldingInstructionsPlayerEditor.cs // Player controls
```

## See Also

- `PaperMesh.cs` - Core folding implementation
- `FoldController.cs` - Interactive fold controls
- Unity Documentation: ScriptableObjects, Custom Editors
