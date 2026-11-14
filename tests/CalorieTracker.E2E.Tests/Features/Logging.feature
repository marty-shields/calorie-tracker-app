Feature: Logging
  The log records what happened, never what I typed.

  Scenario: Startup is logged
    Then the log shows "started on Android API "
    And the log shows "Startup finished in"
    And the log shows "Activity created"
