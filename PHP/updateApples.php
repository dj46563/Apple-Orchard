<?php include "../inc/dbinfo.inc"; ?>
<?php 
    http_response_code(400);


    $con = mysqli_connect(DB_SERVER, DB_USERNAME, DB_PASSWORD, DB_DATABASE);

    //Check that connection worked
    if (mysqli_connect_errno()) {
        die("1. Connect error: " . mysqli_connect_error());
    }

    $jsonObj = json_decode($_POST["json"], true);
    $usernames = $jsonObj["usernames"];
    $apples = $jsonObj["apples"];

    for ($i = 0; $i < count($usernames); $i++) {
        $updateQuery = sprintf("UPDATE players SET apples = %d WHERE username = '%s';", $apples[$i], $usernames[$i]);
        $updateCheck = mysqli_query($con, $updateQuery) or die("2. Update query failed: " . mysqli_error($con));
    }

    http_response_code(200);
    echo("0");
?>