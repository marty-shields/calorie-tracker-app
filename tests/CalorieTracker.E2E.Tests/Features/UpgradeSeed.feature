@upgrade-seed
Feature: Upgrade seed
  The first half of the next release's upgrade check. upgrade-e2e.sh runs this scenario from the last release's own E2E
  project, on that release's own build, and leaves the app installed with these settings; Upgrade.feature then checks
  them in the build installed over it. So keep what it saves as it is: the Dark theme, a daily target of 2100 (set today, after
  1900 set two days ago on the Debug app, which makes two target history rows; the Release app has no clock seam, so both land today), and
  one diary entry (100 g of the first food found by "oat", in Lunch, today) that is also marked as a favourite. That food is the
  one logged before the upgrade, and Upgrade.feature checks it is still in the diary, shown with its title and found by search.
  To check more, save it here and check it in Upgrade.feature only once a release has it. The shards and the Release E2E run this scenario too, so every release
  proves its own seed works.

  @release
  Scenario: Save the settings an upgrade must keep
    Given I have opened Settings
    When I choose the Dark theme
    Then the Dark theme is chosen
    And I go back to the Dashboard
    Given the app clock reads 2 days ago at 12:00
    And I have opened Settings
    When I set the daily target to 1900
    And I go back to the Dashboard
    Given the app clock is the device's
    And I have opened Settings
    When I set the daily target to 2100
    And I go back to the Dashboard
    Given I have added 100 g of the first food found by "oat" to Lunch
    Then Lunch lists 1 entry
    When I tap the Lunch entry
    And I tap the star on Serving
    Then the star on Serving is filled and named "Remove from favourites"
    When I tap Save
    Then I am on the Dashboard
