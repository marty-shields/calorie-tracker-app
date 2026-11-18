Feature: Settings
  The gear on Today opens Settings; back returns to Today. The status bar keeps light icons over the top bar in every theme.

  @smoke @release
  Scenario: The gear on the Dashboard opens Settings
    When I tap Settings on the Dashboard
    Then I am on Settings

  Scenario: Settings shows the app version
    Given I have opened Settings
    Then the app version is listed on Settings

  Scenario: A daily target survives closing and reopening the app
    Given I have opened Settings
    And I have set the daily target to 2100
    When I close the app and open it again
    And I open Settings
    Then the daily target is 2100

  Scenario: Emptying the daily target clears it
    Given I have opened Settings
    And I have set the daily target to 2100
    When I clear the daily target
    And I close the app and open it again
    And I open Settings
    Then the daily target is empty

  Scenario: A daily target of zero is rejected
    Given I have opened Settings
    When I type 0 as the daily target
    Then Settings asks for "Enter a whole number above 0, or clear the field."

  Scenario Outline: Back returns to the Dashboard
    Given I have opened Settings
    When I go back with <how>
    Then I am on the Dashboard
    Examples:
      | how            |
      | the back arrow |
      | Android back   |

  Scenario Outline: The status bar icons stay light on the dark top bar
    Given the phone's dark theme is <phone>
    And I have opened Settings
    When I choose the <app> theme
    Then the status bar icons are light
    Examples:
      | phone | app    |
      | off   | System |
      | off   | Light  |
      | off   | Dark   |
      | on    | System |
      | on    | Light  |
      | on    | Dark   |
