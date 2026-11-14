@upgrade-seed
Feature: Upgrade seed
  The first half of the next release's upgrade check. upgrade-e2e.sh runs this scenario from the last release's own E2E
  project, on that release's own build, and leaves the app installed with these settings; Upgrade.feature then checks
  them in the build installed over it. So keep what it saves as it is. To check more, save it here and check it
  in Upgrade.feature only once a release has it. The shards and the Release E2E run this scenario too, so every release
  proves its own seed works.

  @release
  Scenario: Save the settings an upgrade must keep
    Given I have opened Settings
    When I choose the Dark theme
    Then the Dark theme is chosen
