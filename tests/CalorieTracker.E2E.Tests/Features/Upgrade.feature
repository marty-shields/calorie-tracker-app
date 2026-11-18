@upgrade
Feature: Upgrade
  This build installs over the last release and keeps what that release saved. The last release's own E2E project saves
  it first (UpgradeSeed.feature, through upgrade-e2e.sh), so these steps never drive the last release's screens.
  With no release yet, upgrade-e2e.sh skips this check. The food the last release logged must stay in the diary with its
  amount, show under the title the search row gives it, and be found by search with that title.

  Scenario: The theme, daily target and food logged in the last release survive this build installed over it
    Given the last release is installed with the settings it saved
    When I install this build over it
    Then I am on the Dashboard
    And Lunch lists 1 entry
    And the Lunch entry shows "100 g"
    And I have noted the title of the Lunch entry
    When I tap the Lunch entry
    Then Serving is headed with the noted title
    When I tap Back on Serving in edit mode
    And I open Foods from the Add sheet
    And I search for the noted title
    Then the results include a food titled with the noted title
    When I go back to the Dashboard
    Then I am on the Dashboard
    When I open Settings
    Then the Dark theme is chosen
    And the daily target is 2100
