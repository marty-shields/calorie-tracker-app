Feature: Launch
  The app opens on Today, with its content clear of the status bar and the navigation bar.

  @smoke @release
  Scenario: A fresh install opens on the empty Today
    Then I am on the Dashboard titled "Calorie Tracker"
    And the Dashboard shows "Nothing logged today"
    And the navigation bar shows Today, Add and Trends only
    And no startup error is shown

  Scenario: The title and the bar clear the system bars
    Given I am on the Dashboard
    Then the screen title is below the status bar and the bar is above the navigation bar
