Feature: App lifecycle
  Settings stay when the app closes; Today reloads when the app comes back.

  @smoke @release
  Scenario: A chosen theme survives closing and reopening the app
    Given I have opened Settings
    And I have chosen the Dark theme
    When I close the app and open it again
    And I open Settings
    Then the Dark theme is chosen

  Scenario: The app logs when it comes back
    Given I am on the Dashboard
    When I send the app to the background and bring it back
    Then the log shows "Activity started" once more
