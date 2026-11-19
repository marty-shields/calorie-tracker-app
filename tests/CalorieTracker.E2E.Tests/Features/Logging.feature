Feature: Logging
  The log records what happened, never my personal information.

  Scenario: Startup is logged
    Then the log shows "started on Android API "
    And the log shows "Startup finished in"
    And the log shows "Activity created"

  Scenario: What I search for is never logged
    Given I have opened Foods
    When I search for "zqxjvkw"
    Then Foods shows "No foods match"
    And the log doesn't show "zqxjvkw"

  Scenario: The seed loads without holding up Today
    Then I am on the Dashboard titled "Calorie Tracker"
    And the log shows "Seed version "
    And the log shows " loaded: "
