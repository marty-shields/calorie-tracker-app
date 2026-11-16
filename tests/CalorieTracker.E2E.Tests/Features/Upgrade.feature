@upgrade
Feature: Upgrade
  This build installs over the last release and keeps what that release saved. The last release's own E2E project saves
  it first (UpgradeSeed.feature, through upgrade-e2e.sh), so these steps never drive the last release's screens.
  With no release yet, upgrade-e2e.sh skips this check.

  Scenario: The theme, daily target and diary entry saved in the last release survive this build installed over it
    Given the last release is installed with the settings it saved
    When I install this build over it
    Then I am on the Dashboard
    And Lunch lists 1 entry
    And the Lunch entry shows "100 g"
    When I open Settings
    Then the Dark theme is chosen
    And the daily target is 2100
