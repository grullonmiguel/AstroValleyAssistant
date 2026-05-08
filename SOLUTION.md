# Multiple Matches Banner - Implementation Guide

## Step 1: Remove the Sidebar MultipleMatchesControl

**In `RealAuctionView.xaml`**, remove lines 164-166:
```xml
<!-- DELETE THIS SECTION -->
<Border Grid.Row="2" Visibility="{Binding PropertySelected.HasMultipleMatches, Converter={StaticResource BooleanToVisibilityConverter}, FallbackValue=Collapsed}" Margin="0 16 0 0">
    <local:MultipleMatchesControl SelectMatchCommand="{Binding ScrapeMatchCommand}" SelectedItem="{Binding ElementName=AuctionGrid, Path=SelectedItem}"  />
</Border>
```

## Step 2: Add Styles to UserControl.Resources

Add these styles **after the existing DataTemplates** in `<UserControl.Resources>`:

```xml
<!-- Multiple Matches Banner Styles -->
<Style x:Key="MultipleMatchesBanner.Border" TargetType="Border">
    <Setter Property="Background" Value="#E8EAF6"/>
    <Setter Property="BorderBrush" Value="#5C6BC0"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="CornerRadius" Value="6"/>
    <Setter Property="Padding" Value="16,12"/>
    <Setter Property="Margin" Value="0,8,0,8"/>
</Style>

<Style x:Key="MultipleMatchesBanner.Icon" TargetType="Path">
    <Setter Property="Fill" Value="#5C6BC0"/>
    <Setter Property="Width" Value="24"/>
    <Setter Property="Height" Value="24"/>
    <Setter Property="Stretch" Value="Uniform"/>
    <Setter Property="VerticalAlignment" Value="Top"/>
    <Setter Property="Margin" Value="0,2,12,0"/>
</Style>

<Style x:Key="MultipleMatchesBanner.Title" TargetType="TextBlock">
    <Setter Property="FontSize" Value="14"/>
    <Setter Property="FontWeight" Value="SemiBold"/>
    <Setter Property="Foreground" Value="#1A237E"/>
    <Setter Property="TextWrapping" Value="Wrap"/>
</Style>

<Style x:Key="MultipleMatchesBanner.Subtitle" TargetType="TextBlock">
    <Setter Property="FontSize" Value="12"/>
    <Setter Property="Foreground" Value="#5C6BC0"/>
    <Setter Property="Margin" Value="0,4,0,0"/>
    <Setter Property="TextWrapping" Value="Wrap"/>
</Style>

<Style x:Key="MultipleMatchesBanner.Button" TargetType="Button">
    <Setter Property="Background" Value="White"/>
    <Setter Property="BorderBrush" Value="#5C6BC0"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="Foreground" Value="#5C6BC0"/>
    <Setter Property="Padding" Value="16,8"/>
    <Setter Property="FontSize" Value="13"/>
    <Setter Property="FontWeight" Value="Medium"/>
    <Setter Property="Cursor" Value="Hand"/>
    <Setter Property="VerticalAlignment" Value="Center"/>
    <Setter Property="Template">
        <Setter.Value">
            <ControlTemplate TargetType="Button">
                <Border Background="{TemplateBinding Background}"
                        BorderBrush="{TemplateBinding BorderBrush}"
                        BorderThickness="{TemplateBinding BorderThickness}"
                        CornerRadius="4"
                        Padding="{TemplateBinding Padding}">
                    <Grid>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="*"/>
                            <ColumnDefinition Width="Auto"/>
                        </Grid.ColumnDefinitions>
                        <ContentPresenter Grid.Column="0" 
                                        HorizontalAlignment="Center" 
                                        VerticalAlignment="Center"/>
                        <Path Grid.Column="1" 
                              Data="M7 10L12 15L17 10H7Z" 
                              Fill="{TemplateBinding Foreground}"
                              Width="12" 
                              Height="12" 
                              Stretch="Uniform"
                              Margin="8,0,0,0"/>
                    </Grid>
                </Border>
                <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True">
                        <Setter Property="Background" Value="#F5F5F5"/>
                    </Trigger>
                    <Trigger Property="IsPressed" Value="True">
                        <Setter Property="Background" Value="#EEEEEE"/>
                    </Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>

<!-- Info Icon Geometry -->
<Geometry x:Key="Icon.Info">M12,2C6.48,2 2,6.48 2,12C2,17.52 6.48,22 12,22C17.52,22 22,17.52 22,12C22,6.48 17.52,2 12,2M13,17H11V11H13V17M13,9H11V7H13V9Z</Geometry>
```

## Step 3: Add DataGrid RowDetailsTemplate

Replace the DataGrid section (starting around line 240) with this updated version that includes the RowDetailsTemplate:

```xml
<DataGrid Grid.Row="1" x:Name="AuctionGrid" 
          ItemsSource="{Binding PropertyRecords}" 
          SelectedItem="{Binding PropertySelected, Mode=TwoWay}"
          RowDetailsVisibilityMode="VisibleWhenSelected">
    
    <!-- Row Details: Multiple Matches Banner -->
    <DataGrid.RowDetailsTemplate>
        <DataTemplate>
            <Border Style="{StaticResource MultipleMatchesBanner.Border}"
                    Visibility="{Binding HasMultipleMatches, Converter={StaticResource BooleanToVisibilityConverter}}">
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="Auto"/>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="Auto"/>
                    </Grid.ColumnDefinitions>

                    <!-- Info Icon -->
                    <Path Grid.Column="0" 
                          Data="{StaticResource Icon.Info}"
                          Style="{StaticResource MultipleMatchesBanner.Icon}"/>

                    <!-- Message Content -->
                    <StackPanel Grid.Column="1" VerticalAlignment="Center">
                        <TextBlock Style="{StaticResource MultipleMatchesBanner.Title}">
                            <Run Text="{Binding Matches.Count, Mode=OneWay}"/>
                            <Run Text="matches found for parcel"/>
                            <Run Text="{Binding ParcelId, Mode=OneWay}"/>
                        </TextBlock>
                        <TextBlock Text="Select the correct property to resolve this match."
                                   Style="{StaticResource MultipleMatchesBanner.Subtitle}"/>
                    </StackPanel>

                    <!-- View Matches Button -->
                    <Button Grid.Column="2" 
                            Content="View Matches"
                            Style="{StaticResource MultipleMatchesBanner.Button}"
                            Command="{Binding DataContext.ViewMatchesCommand, RelativeSource={RelativeSource AncestorType=DataGrid}}"
                            CommandParameter="{Binding}"/>
                </Grid>
            </Border>
        </DataTemplate>
    </DataGrid.RowDetailsTemplate>

    <DataGrid.Columns>
        <DataGridTemplateColumn Header="Status" Width="85" CellTemplate="{StaticResource StatusBadgeTemplate}"/>
        <DataGridTextColumn Header="Parcel ID" Binding="{Binding ParcelId}" Width="Auto"/>
        <DataGridTextColumn Header="Address" Binding="{Binding Address}" Width="*"/>
        <DataGridTextColumn Header="Owner" Binding="{Binding Owner}" Width="Auto"/>
        <DataGridTextColumn Header="Assessed" Binding="{Binding AssessedValue, StringFormat={}{0:C}}" Width="100"/>
        <DataGridTextColumn Header="Opening Bid" Binding="{Binding Bid, StringFormat={}{0:C}}" Width="100"/>
        <DataGridTextColumn Header="Acres" Binding="{Binding Acres, StringFormat={}{0:N4}}" Width="100"/>
    </DataGrid.Columns>
</DataGrid>
```

## Step 4: Add ViewMatchesCommand to ViewModel

In `RealAuctionViewModel.cs` (or `PropertyScraperViewModelBase.cs`), add:

```csharp
[RelayCommand]
private void ViewMatches(PropertyDataViewModel property)
{
    System.Diagnostics.Debug.WriteLine($"View Matches clicked for: {property.ParcelId}");
    // TODO: Next step - show matches dialog/panel
}
```

## Summary of Changes:

1. ✅ Removed sidebar MultipleMatchesControl
2. ✅ Added inline banner with info icon, message, and button
3. ✅ All styles defined in Resources
4. ✅ Banner shows only for selected row with multiple matches
5. ✅ Matches the design from the image

The banner will appear below the selected row when `HasMultipleMatches` is true, showing the count and parcel ID dynamically.
