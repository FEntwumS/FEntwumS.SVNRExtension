--! \file breakpoint_controller.vhd
--! \brief Kontrolliert das Erzeugen/L�schen von Breakpoints, und �berpr�ft ob ein Breakpoint im derzeitigen CPU Takt vorliegt.

library IEEE;
use IEEE.STD_LOGIC_1164.ALL;

--! \brief Kontrolliert das Erzeugen/L�schen von Breakpoints, und �berpr�ft ob ein Breakpoint im derzeitigen CPU Takt vorliegt.
--! 
--! Die State-Machine dient zur Koordination von Erzeugung/L�schen der Breakpoints.
--! Die Breakpoints werden in einem Array gespeichert, es k�nnen maximal 16 Breakpoints gleichzeitig bestehen. 
--! 0XFFFF beschreibt einen leeren/nicht aktivierten Breakpoint in der Liste. 
--! So kann ein Breakpoint bei bspw. Addresse *0x0000* erzeugt werden. Die Addresse *0xFFFF* spielt im SVNR keine Rolle, da dessen Addressraum nur *1024* oder *0x0400* Addressen umfasst.
--! Der Index der ersten freuen Stelle des Arrays wird stets gespeichert, sodass Breakpoints an jeder Stelle des Arrays dynamisch gel�scht/hinzugef�gt werden k�nnen. 

entity Breakpoint_Controller is
    Port ( 
           i_clk                   : in STD_LOGIC;
           i_program_counter       : in std_logic_vector(15 downto 0);      --! Program Counter des SVNR
           i_breakpoint_add        : in STD_LOGIC;                          --! Startet Hinzufuegen eines neuen Breakpoints 
           i_breakpoint_delete     : in STD_LOGIC;                          --! Startet Loeschen eines neuen Breakpoints
           i_breakpoint_value      : in std_logic_vector(15 downto 0);      --! Breakpointadresse des hinzuzufuegenden (`i_breakpoint_add`), oder des zu loeschenden (`i_breakpoint_delete`) Breakpoints
           i_breakpoint_enable     : in std_logic;                          --! Nicht in Nutzung! Soll Breakpoints insgesamt aktivieren/deaktivieren
           i_run                   : in std_logic;                          --! Verwertet das run Signal aus dem Decoder und erzeugt mit der Breakpoint Logik `o_cpu_en`
           o_cpu_en                : out STD_LOGIC := '0';                  --! Cpu_enable Signal fuer die SVNR CPU 
           o_cpu_halt              : out std_logic;                         --! Indiziert einen gefundenen Breakpoint
           o_edit_done             : out STD_LOGIC                          --! Indiziert Loeschen/Hinzufuegen abgeschlossen
           );
end Breakpoint_Controller;

architecture Behavioral of Breakpoint_Controller is
    constant ARRAY_LENGTH : INTEGER := 16;  -- !Maximum number of breakpoints!
    type BreakpointArray is array (0 to ARRAY_LENGTH - 1 ) of std_logic_vector(15 downto 0);
    signal number_breakpoints : INTEGER:= 0;

    signal PC                   : std_logic_vector(15 downto 0) := (others => '0');
    signal breakpoints          : BreakpointArray := (others => (others => '1')); -- Initialize with 0xFFFF
    signal breakpoint_enable    : boolean := true;
    signal breakpoint_found     : boolean := false;
    signal edit_done            : std_logic := '0';
    signal s_empty_index        : integer := 0;
    signal s_last_halt          : std_logic_vector(15 downto 0) := (others => '0');

    type STATE is (
        z_RUNNING,
        z_HALT,
        z_ADD,
        z_SEARCH_EMPTY,
        z_DELETE
    );

    signal fsm_state_bp_controller: STATE := z_HALT;
    
begin
    process(i_clk, i_breakpoint_add, i_breakpoint_delete)

    begin
        PC <= i_program_counter;
        o_edit_done <= edit_done;
        
        if rising_edge(i_clk) then
            CASE fsm_state_bp_controller is
                WHEN z_RUNNING => 
                    if i_run = '1' then
                        for i in 0 to ARRAY_LENGTH -1 loop
                            if PC = breakpoints(i) and breakpoint_enable then
                                -- Halt the processor when the breakpoint is reached
                                s_last_halt <= PC; 
                                o_cpu_halt <= '1';
                                o_cpu_en <= '0';
                                breakpoint_found <= false;
                                fsm_state_bp_controller <= z_HALT;
                                exit;  -- Exit the loop if a breakpoint is found
                            else
                                -- Allow normal processor operation
                                o_cpu_en <= '1';
                            end if;
                        end loop;  -- End of the loop
                    else
                        o_cpu_en <= '0';
                        s_last_halt <= PC; 
                        fsm_state_bp_controller <= z_HALT;
                    end if;
                
                WHEN z_HALT =>
                    o_cpu_halt <= '0';
                    edit_done <= '0';
                    
                    if i_run = '1' then
                        o_cpu_en <= '1';
                        -- let cpu run until the program counter changes, then jump into bp check routine again
                        if s_last_halt /= PC then
                            fsm_state_bp_controller <= z_RUNNING; 
                        end if;
                    end if;

                    if i_breakpoint_add = '1' and number_breakpoints <= ARRAY_LENGTH - 1 then
                        fsm_state_bp_controller <= z_ADD;
                    end if;

                    if i_breakpoint_delete = '1' then
                        fsm_state_bp_controller <= z_DELETE;
                    end if;
                
                WHEN z_ADD => 
                    breakpoints(s_empty_index) <= i_breakpoint_value;
                    number_breakpoints <= number_breakpoints + 1;
                    fsm_state_bp_controller <= z_SEARCH_EMPTY;

                WHEN z_SEARCH_EMPTY => 
                    -- find next empty index after inserting the new bp
                    if (number_breakpoints >= ARRAY_LENGTH) then 
                        s_empty_index <= -1;
                        fsm_state_bp_controller <= z_HALT;
                    else
                        for i in 0 to ARRAY_LENGTH -1 loop
                            if breakpoints(i) = X"FFFF" then
                                s_empty_index <=  i;
                                edit_done <= '1';
                                fsm_state_bp_controller <= z_HALT;
                                exit;
                            end if;
                        end loop;
                    end if;

                WHEN z_DELETE =>
                    -- empty_index := -1;
                    -- edit_done <= '0';
                    for i in 0 to ARRAY_LENGTH -1 loop
                        if breakpoints(i) = i_breakpoint_value and i_breakpoint_value /= X"FFFF" then
                            breakpoints(i) <= X"FFFF";
                            s_empty_index <= i;
                            number_breakpoints <=  number_breakpoints - 1;
                            edit_done <= '1';
                            fsm_state_bp_controller <= z_HALT;
                            exit;
                        end if;
                    end loop;
            END CASE;
        end if;

    end process;

end Behavioral;
