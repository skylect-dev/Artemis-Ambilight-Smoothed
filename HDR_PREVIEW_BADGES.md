# HDR/SDR Preview Badges Implementation

## Overview
Added visual HDR/SDR indicator badges to all preview images in the UI to clearly show whether native HDR capture is being used or standard SDR capture.

## Changes Made

### 1. New Converter Classes
Created two new value converters for data binding:

#### **Converters/HdrTextConverter.cs** (NEW)
- Converts `bool IsHdr` property to "HDR" or "SDR" text
- Used for badge text display
- Static instance for efficient XAML binding

#### **Converters/HdrColorConverter.cs** (NEW)
- Converts `bool IsHdr` property to badge background color
- Gold (ARGB: 230, 255, 215, 0) for HDR
- Gray (ARGB: 200, 100, 100, 100) for SDR
- Static instance for efficient XAML binding

### 2. UI Badge Overlays
Added HDR/SDR badge overlays to all preview locations:

#### **Screens/CaptureRegionDisplayView.axaml** (MODIFIED)
- Added badge overlay to result preview (processed capture)
- Badge positioned in top-right corner with 8px margin
- Bound to `DisplayPreview.IsHdr` property
- Shows "HDR" in gold or "SDR" in gray

#### **Screens/CaptureScreenView.axaml** (MODIFIED)
- Added namespace import for converters
- Added badge overlay to source preview (display selection)
- Same styling and positioning as result preview
- Shows capture method for selected display

#### **Screens/CaptureRegionEditorView.axaml** (MODIFIED)
- Added namespace import for converters
- Added badge overlay to region editor preview
- Consistent badge styling across all previews
- Shows capture method while editing capture region

### 3. Data Flow Updates
Updated ViewModel constructors to properly pass HDR capture through the chain:

#### **Screens/DisplayPreview.cs** (MODIFIED)
- Both constructors now accept optional `HdrScreenCapture?` parameter
- Simple constructor: `DisplayPreview(Display, bool highQuality, HdrScreenCapture?)`
- Full constructor: `DisplayPreview(Display, AmbilightSmoothedCaptureProperties, HdrScreenCapture?)`
- Initializes `IsHdr` property based on HDR capture availability
- Uses HDR capture dimensions for preview bitmap when available

#### **Screens/CaptureScreenViewModel.cs** (MODIFIED)
- Added using statement for `Services` namespace
- Constructor accepts optional `HdrScreenCapture?` parameter
- Passes HDR capture to `DisplayPreview` constructor

#### **Screens/CaptureRegionEditorViewModel.cs** (MODIFIED)
- Added using statement for `Services` namespace
- Constructor accepts optional `HdrScreenCapture?` parameter
- Passes HDR capture to `DisplayPreview` constructor

#### **Screens/CapturePropertiesViewModel.cs** (MODIFIED)
- Passes `AmbilightSmoothedLayerBrush.HdrCapture` when creating:
  - `CaptureScreenViewModel` instances (display list)
  - `CaptureRegionEditorViewModel` (region editor)
  - `CaptureRegionDisplayViewModel` (result preview)
- Ensures all previews use the same HDR capture instance

## Badge Design

### Visual Styling
- **Position**: Top-right corner with 8px margin
- **Shape**: Rounded corners (3px radius)
- **Padding**: 6px horizontal, 2px vertical
- **Font**: Bold, 10pt, white text
- **Colors**:
  - HDR: Gold background (#E6FFD700)
  - SDR: Gray background (#C8646464)

### Data Binding
```xml
<Border.Background>
    <SolidColorBrush Color="{Binding DisplayPreview.IsHdr, Converter={x:Static converters:HdrColorConverter.Instance}}" />
</Border.Background>
<TextBlock Text="{Binding DisplayPreview.IsHdr, Converter={x:Static converters:HdrTextConverter.Instance}}"
           Foreground="White"
           FontWeight="Bold"
           FontSize="10" />
```

## How It Works

1. **HDR Detection**: When the brush initializes, it attempts to create an `HdrScreenCapture` instance
2. **Capture Propagation**: The HDR capture instance (or null for SDR) is passed through the ViewModel chain
3. **Preview Creation**: `DisplayPreview` receives the HDR capture and sets `IsHdr = (hdrCapture != null)`
4. **UI Display**: Badge converters transform `IsHdr` boolean into appropriate text and colors
5. **Visual Feedback**: User sees "HDR" (gold) or "SDR" (gray) on all preview images

## Preview Locations

All three preview locations now show HDR/SDR badges:

1. **Source Preview** (CaptureScreenView): Shows capture method for selected display
2. **Region Editor** (CaptureRegionEditorView): Shows capture method while editing region
3. **Result Preview** (CaptureRegionDisplayView): Shows capture method for processed output

## Benefits

- **Clear Visual Confirmation**: Immediate feedback on whether HDR capture is active
- **Consistent Design**: Same badge styling across all preview locations
- **Reactive Updates**: Badges automatically update if capture method changes
- **Non-Intrusive**: Small corner badges don't obscure preview content
- **Color Coding**: Gold/gray colors provide quick visual distinction

## Testing

To verify the implementation:

1. **HDR Display**: Connect HDR-capable display in HDR mode
   - All preview badges should show "HDR" in gold
   - Previews should show actual HDR capture data

2. **SDR Display**: Use standard SDR display
   - All preview badges should show "SDR" in gray
   - Previews should show standard ScreenCapture.NET data

3. **Mixed Scenario**: If brush detects HDR but falls back to SDR
   - Badges should accurately reflect actual capture method used
   - Helps diagnose HDR initialization issues

## Build Output

- Package: `AmbilightSmoothed-HDR.zip` (590,907 bytes)
- Build: Release configuration, net10.0-windows
- Warnings: 37 (mostly nullability, no errors)
