Feature: Launch
  The app opens on the Dashboard, with its content clear of the status bar and the navigation bar.

  @smoke @release
  Scenario: A fresh install opens on the empty Dashboard
    Then I am on the Dashboard titled "Calorie Tracker"
    And the Dashboard shows "Nothing here yet"
    And I can open the Dashboard from the navigation bar
    And no startup error is shown

  Scenario: The title and the tabs clear the system bars
    Given I am on the Dashboard
    Then the screen title is below the status bar and the tabs are above the navigation bar
