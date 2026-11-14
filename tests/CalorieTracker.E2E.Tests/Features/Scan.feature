Feature: Scan
  The navigation bar opens Scan, which asks for the camera. Without it Scan says so. A scanned product needs a real barcode in front of a camera, so the Found and Not found states are covered by bUnit, not here.

  Scenario: The navigation bar opens Scan
    Given I am on the Dashboard
    When I open Scan from the navigation bar
    Then I am on Scan

  Scenario: A refused camera permission shows the camera-unavailable message
    Given I am on the Dashboard
    When I open Scan from the navigation bar
    And I refuse the camera permission
    Then Scan says the camera is unavailable
    And Try again is offered on Scan

  Scenario: Android back leaves Scan
    Given I am on the Dashboard
    And I have opened Scan
    And I have refused the camera permission
    When I press Android back
    Then I am on the Dashboard
