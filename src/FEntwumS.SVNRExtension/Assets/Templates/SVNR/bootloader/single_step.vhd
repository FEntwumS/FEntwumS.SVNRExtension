--! \file single_step.vhd
--! \brief Koordiniert das Einzelschrittverfahren der SVNR CPU   
--! 
--! Diese Komponente ist nicht getestet, und somit auch sicher nicht korrekt implementiert! 
--! Das Konzept ist aber recht simpel: 
--! Die Komponente erzeugt im Single-Step Modus das cpu_en Signal, welches die CPU des SVNR aktiviert.
--! 1. Mit `i_run` wird die CPU gestartet und `o_cpu_en` wird auf `'1'` gesetzt. 
--! 2. Sobald die CPU einen Schritt ausgeführt hat/ der Program Counter sich um 1 inkrementiert (angezeigt durch das Signal `i_cpu_step_fin`), wird `o_cpu_en` auf `'0'` gesetzt.
--! 3. Jetzt wartet der Single Step wieder auf ein `i_run` um den nächsten Step auszuführen. 


library ieee;
use ieee.std_logic_1164.all;


--! \brief Koordiniert das Einzelschrittverfahren der SVNR CPU   
--! 
--! Diese Komponente ist nicht getestet, und somit auch sicher nicht korrekt implementiert! 
--! Das Konzept ist aber recht simpel: 
--! Die Komponente erzeugt im Single-Step Modus das cpu_en Signal, welches die CPU des SVNR aktiviert.
--! 1. Mit `i_run` wird die CPU gestartet und `o_cpu_en` wird auf `'1'` gesetzt. 
--! 2. Sobald die CPU einen Schritt ausgeführt hat/ der Program Counter sich um 1 inkrementiert (angezeigt durch das Signal `i_cpu_step_fin`), wird `o_cpu_en` auf `'0'` gesetzt.
--! 3. Jetzt wartet der Single Step wieder auf ein `i_run` um den nächsten Step auszuführen. 
entity single_step is
    port (
        i_run            : in  std_logic;
        i_cpu_step_fin   : in  std_logic;
        o_cpu_en         : out std_logic
    );
end entity single_step;

architecture Behavioral of single_step is
    type State is (IDLE, RUNNING, WAIT_FOR_FINISH);
    signal state_machine : State := IDLE;
    signal internal_cpu_en : std_logic := '0';
begin
    process(i_run, i_cpu_step_fin)
    begin
        case state_machine is
            when IDLE =>
                if i_run = '1' then
                    -- Start single-step mode
                    internal_cpu_en <= '1';
                    state_machine <= RUNNING;
                end if;
                
            when RUNNING =>
                -- Continue running until i_cpu_step_fin is asserted
                if i_cpu_step_fin = '1' then
                    state_machine <= WAIT_FOR_FINISH;
                end if;
                
            when WAIT_FOR_FINISH =>
                -- Wait for i_cpu_step_fin to go low
                if i_cpu_step_fin = '0' then
                    internal_cpu_en <= '0';
                    state_machine <= IDLE;
                end if;
        end case;
    end process;

    -- Output the internal_cpu_en signal
    o_cpu_en <= internal_cpu_en;

end architecture Behavioral;
