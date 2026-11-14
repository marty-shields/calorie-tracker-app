Feature: Logging
  The log records what happened, never what I typed.

  Scenario: Startup is logged
    Then the log shows "started on Android API "
    And the log shows "Startup finished in"
    And the log shows "Activity created"

  Scenario: The seed loads without holding up the Dashboard
    Then I am on the Dashboard titled "Calorie Tracker"
    And the log shows "Seed version "
    And the log shows " loaded: "
